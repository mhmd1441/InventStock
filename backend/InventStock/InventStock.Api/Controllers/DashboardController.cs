using InventStock.Application.DTOs.Dashboard;
using InventStock.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventStock.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public DashboardController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet]
    [ProducesResponseType(typeof(CounterDashboardDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CounterDashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await _dashboard.GetCounterSummaryAsync(cancellationToken));

    [HttpGet("/api/admin/dashboard")]
    [Authorize(Roles = InventStock.Application.Common.RoleNames.Admin)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DashboardSummaryDto>> GetAdmin(CancellationToken cancellationToken) =>
        Ok(await _dashboard.GetSummaryAsync(cancellationToken));
}
