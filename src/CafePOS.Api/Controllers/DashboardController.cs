using CafePOS.Api.Services;
using CafePOS.Core.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CafePOS.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly ReportService _reports;

    public DashboardController(ReportService reports) => _reports = reports;

    [HttpGet]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken ct) => Ok(await _reports.GetDashboardAsync(ct));
}
