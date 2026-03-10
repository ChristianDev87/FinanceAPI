using System.Security.Claims;
using FinanceAPI.DTOs.Statistics;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : ControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    private int UserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("monthly")]
    public async Task<ActionResult<IEnumerable<MonthlyStatDto>>> GetMonthly(
        [FromQuery] int year = 0)
    {
        if (year == 0) year = DateTime.UtcNow.Year;
        return Ok(await _statisticsService.GetMonthlyAsync(UserId, year));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<CategoryStatDto>>> GetByCategory(
        [FromQuery] int month = 0,
        [FromQuery] int year = 0,
        [FromQuery] string? type = null)
    {
        if (month == 0) month = DateTime.UtcNow.Month;
        if (year == 0) year = DateTime.UtcNow.Year;
        return Ok(await _statisticsService.GetByCategoryAsync(UserId, month, year, type));
    }
}
