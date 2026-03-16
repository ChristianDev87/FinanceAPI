using System.ComponentModel.DataAnnotations;
using FinanceAPI.DTOs.Statistics;
using FinanceAPI.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAPI.Controllers;

[ApiController]
[Route("api/statistics")]
[Authorize]
public class StatisticsController : AuthenticatedControllerBase
{
    private readonly IStatisticsService _statisticsService;

    public StatisticsController(IStatisticsService statisticsService)
    {
        _statisticsService = statisticsService;
    }

    [HttpGet("years")]
    public async Task<ActionResult<IEnumerable<int>>> GetAvailableYears()
    {
        return Ok(await _statisticsService.GetAvailableYearsAsync(UserId));
    }

    [HttpGet("monthly")]
    public async Task<ActionResult<IEnumerable<MonthlyStatDto>>> GetMonthly(
        [FromQuery][Range(1900, 2100)] int year = 0)
    {
        if (year == 0)
        {
            year = DateTime.UtcNow.Year;
        }

        return Ok(await _statisticsService.GetMonthlyAsync(UserId, year));
    }

    [HttpGet("categories")]
    public async Task<ActionResult<IEnumerable<CategoryStatDto>>> GetByCategory(
        [FromQuery][Range(1, 12)] int month = 0,
        [FromQuery][Range(1900, 2100)] int year = 0,
        [FromQuery][RegularExpression("^(income|expense)$", ErrorMessage = "type must be 'income' or 'expense'.")] string? type = null)
    {
        if (month == 0)
        {
            month = DateTime.UtcNow.Month;
        }

        if (year == 0)
        {
            year = DateTime.UtcNow.Year;
        }

        return Ok(await _statisticsService.GetByCategoryAsync(UserId, month, year, type));
    }
}
