using CafePOS.Api.Data;
using CafePOS.Core.BusinessRules;
using CafePOS.Core.Contracts;
using CafePOS.Core.Entities;
using CafePOS.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Services;

public class ShiftService
{
    private readonly AppDbContext _db;
    private readonly Security.CurrentUser _currentUser;
    private readonly AuditService _audit;

    public ShiftService(AppDbContext db, Security.CurrentUser currentUser, AuditService audit)
    {
        _db = db;
        _currentUser = currentUser;
        _audit = audit;
    }

    public Task<Shift?> GetOpenShiftAsync(Guid userId, CancellationToken ct = default) =>
        _db.Shifts.Include(s => s.Expenses).Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == ShiftStatus.Open, ct);

    public async Task<Shift> OpenAsync(decimal openingCash, CancellationToken ct = default)
    {
        if (openingCash < 0) throw new InvalidOperationException("Opening cash cannot be negative.");
        var existing = await GetOpenShiftAsync(_currentUser.UserId, ct);
        if (existing is not null) throw new InvalidOperationException("You already have an open shift. Close it before opening a new one.");

        var shift = new Shift
        {
            UserId = _currentUser.UserId,
            OpeningCash = openingCash,
            ExpectedCash = openingCash,
            Status = ShiftStatus.Open
        };
        _db.Shifts.Add(shift);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ShiftOpened", nameof(Shift), shift.Id.ToString(), new { openingCash }, ct);
        return shift;
    }

    public async Task<ShiftDto> GetDtoAsync(Shift shift, CancellationToken ct = default)
    {
        var totals = await CalculateAsync(shift, ct);
        return new ShiftDto(shift.Id, shift.UserId, shift.User?.FullName ?? string.Empty, shift.OpenedAt, shift.ClosedAt,
            shift.OpeningCash, shift.Status == ShiftStatus.Closed ? shift.ExpectedCash : totals.ExpectedCash,
            shift.CountedCash, shift.Difference, shift.Status, shift.Notes,
            totals.CashSales, totals.CardSales, totals.BankSales, totals.CashRefunds, totals.Expenses);
    }

    public async Task<ShiftDto?> CurrentAsync(CancellationToken ct = default)
    {
        var shift = await GetOpenShiftAsync(_currentUser.UserId, ct);
        return shift is null ? null : await GetDtoAsync(shift, ct);
    }

    public async Task<Shift> CloseAsync(Guid shiftId, decimal countedCash, string? notes, CancellationToken ct = default)
    {
        var shift = await _db.Shifts.Include(s => s.User).Include(s => s.Expenses)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw new KeyNotFoundException("Shift not found.");
        if (shift.Status == ShiftStatus.Closed) throw new InvalidOperationException("This shift is already closed.");

        if (_currentUser.Role == UserRole.Cashier && shift.UserId != _currentUser.UserId)
            throw new UnauthorizedAccessException("You can only close your own shift.");
        if (countedCash < 0) throw new InvalidOperationException("Counted cash cannot be negative.");
        if (shift.UserId != _currentUser.UserId && !_currentUser.IsInRole(UserRole.Manager, UserRole.Administrator))
            throw new UnauthorizedAccessException("Only a manager or administrator can close another user's shift.");

        var totals = await CalculateAsync(shift, ct);
        shift.ExpectedCash = totals.ExpectedCash;
        shift.CountedCash = countedCash;
        shift.Difference = ShiftMath.Difference(countedCash, totals.ExpectedCash);
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAt = DateTimeOffset.UtcNow;
        shift.ClosedByUserId = _currentUser.UserId;
        shift.Notes = string.IsNullOrWhiteSpace(notes) ? shift.Notes : notes;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("ShiftClosed", nameof(Shift), shift.Id.ToString(),
            new { shift.OpeningCash, shift.ExpectedCash, shift.CountedCash, shift.Difference, notes }, ct);
        return shift;
    }

    public async Task<Shift> ReopenAsync(Guid shiftId, string reason, CancellationToken ct = default)
    {
        if (!_currentUser.IsInRole(UserRole.Administrator))
            throw new UnauthorizedAccessException("Only an administrator may reopen a closed shift.");

        var shift = await _db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId, ct)
            ?? throw new KeyNotFoundException("Shift not found.");
        if (shift.Status == ShiftStatus.Open) throw new InvalidOperationException("This shift is already open.");

        shift.Status = ShiftStatus.Open;
        shift.ClosedAt = null;
        shift.Notes = $"{shift.Notes}\nReopened: {reason}".Trim();
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ShiftReopened", nameof(Shift), shift.Id.ToString(), new { reason }, ct);
        return shift;
    }

    public async Task<ShiftExpense> AddExpenseAsync(string description, decimal amount, string? category,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description)) throw new InvalidOperationException("Description is required.");
        if (amount <= 0) throw new InvalidOperationException("Expense amount must be greater than zero.");

        var shift = await GetOpenShiftAsync(_currentUser.UserId, ct)
            ?? throw new InvalidOperationException("Open a shift before recording an expense.");

        var expense = new ShiftExpense
        {
            ShiftId = shift.Id,
            Description = description.Trim(),
            Amount = amount,
            Category = category,
            UserId = _currentUser.UserId
        };
        _db.ShiftExpenses.Add(expense);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ShiftExpense", nameof(ShiftExpense), expense.Id.ToString(),
            new { description, amount, category }, ct);
        return expense;
    }

    public async Task<List<ShiftDto>> ListAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = _db.Shifts.AsNoTracking().Include(s => s.User).Include(s => s.Expenses).AsQueryable();
        if (from.HasValue) query = query.Where(s => s.OpenedAt >= from.Value);
        if (to.HasValue) query = query.Where(s => s.OpenedAt <= to.Value);
        var shifts = await query.OrderByDescending(s => s.OpenedAt).Take(200).ToListAsync(ct);
        var result = new List<ShiftDto>();
        foreach (var shift in shifts) result.Add(await GetDtoAsync(shift, ct));
        return result;
    }

    private async Task<ShiftTotals> CalculateAsync(Shift shift, CancellationToken ct)
    {
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.ShiftId == shift.Id)
            .Select(p => new { p.Method, p.Amount })
            .ToListAsync(ct);

        var cashSales = Money.Round(payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount));
        var cardSales = Money.Round(payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount));
        var bankSales = Money.Round(payments.Where(p => p.Method == PaymentMethod.BankTransfer).Sum(p => p.Amount));

        var refunds = await (from r in _db.Refunds.AsNoTracking()
                             join p in _db.Payments.AsNoTracking() on r.PaymentId equals p.Id
                             where r.ShiftId == shift.Id
                             select new { r.Amount, p.Method }).ToListAsync(ct);
        var cashRefunds = Money.Round(refunds.Where(r => r.Method == PaymentMethod.Cash).Sum(r => r.Amount));

        var expenses = Money.Round(await _db.ShiftExpenses.AsNoTracking()
            .Where(e => e.ShiftId == shift.Id).SumAsync(e => (decimal?)e.Amount, ct) ?? 0);

        var expected = ShiftMath.ExpectedCash(shift.OpeningCash, cashSales, cashRefunds, expenses);
        return new ShiftTotals(cashSales, cardSales, bankSales, cashRefunds, expenses, expected);
    }

    private record ShiftTotals(decimal CashSales, decimal CardSales, decimal BankSales, decimal CashRefunds,
        decimal Expenses, decimal ExpectedCash);
}
