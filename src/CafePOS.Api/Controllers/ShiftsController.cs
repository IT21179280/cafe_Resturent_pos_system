using CafePOS.Api.Data;
using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/shifts")]
[Authorize]
public class ShiftsController : ControllerBase
{
    private readonly ShiftService _shifts;
    private readonly AppDbContext _db;

    public ShiftsController(ShiftService shifts, AppDbContext db)
    {
        _shifts = shifts;
        _db = db;
    }

    [HttpGet("current")]
    public async Task<ActionResult<ShiftDto>> Current(CancellationToken ct)
    {
        var shift = await _shifts.CurrentAsync(ct);
        return shift is null ? NoContent() : Ok(shift);
    }

    [HttpPost("open")]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<ShiftDto>> Open([FromBody] OpenShiftRequest request, CancellationToken ct)
    {
        var shift = await _shifts.OpenAsync(request.OpeningCash, ct);
        return Ok(await _shifts.GetDtoAsync(shift, ct));
    }

    [HttpPost("{id:guid}/close")]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<ShiftDto>> Close(Guid id, [FromBody] CloseShiftRequest request, CancellationToken ct)
    {
        var shift = await _shifts.CloseAsync(id, request.CountedCash, request.Notes, ct);
        var full = await _db.Shifts.AsNoTracking().Include(s => s.User).Include(s => s.Expenses)
            .FirstAsync(s => s.Id == shift.Id, ct);
        return Ok(await _shifts.GetDtoAsync(full, ct));
    }

    [HttpPost("{id:guid}/reopen")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ShiftDto>> Reopen(Guid id, [FromBody] CancelOrderRequest request, CancellationToken ct)
    {
        var shift = await _shifts.ReopenAsync(id, request.Reason, ct);
        var full = await _db.Shifts.AsNoTracking().Include(s => s.User).Include(s => s.Expenses)
            .FirstAsync(s => s.Id == shift.Id, ct);
        return Ok(await _shifts.GetDtoAsync(full, ct));
    }

    [HttpPost("{id:guid}/expenses")]
    [Authorize(Roles = "Administrator,Manager,Cashier")]
    public async Task<ActionResult<ShiftDto>> AddExpense(Guid id, [FromBody] ShiftExpenseRequest request, CancellationToken ct)
    {
        var current = await _shifts.CurrentAsync(ct);
        if (current is null || current.Id != id)
            return BadRequest(new ApiErrorResponse("Expenses can only be added to your open shift."));

        await _shifts.AddExpenseAsync(request.Description, request.Amount, request.Category, ct);
        var shift = await _db.Shifts.AsNoTracking().Include(s => s.User).Include(s => s.Expenses)
            .FirstAsync(s => s.Id == id, ct);
        return Ok(await _shifts.GetDtoAsync(shift, ct));
    }

    [HttpGet]
    [Authorize(Roles = "Administrator,Manager")]
    public async Task<ActionResult<List<ShiftDto>>> List([FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to,
        CancellationToken ct) => Ok(await _shifts.ListAsync(from?.ToUniversalTime(), to?.ToUniversalTime(), ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ShiftDto>> Get(Guid id, CancellationToken ct)
    {
        var shift = await _db.Shifts.AsNoTracking().Include(s => s.User).Include(s => s.Expenses)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        return shift is null ? NotFound() : Ok(await _shifts.GetDtoAsync(shift, ct));
    }
}
