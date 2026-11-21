using itsm.Application.Common.Interfaces;
using itsm.Application.Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace itsm.WebApi.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsController(IReportService reportService) : ControllerBase
{
    [HttpGet]
    [EndpointSummary("Получить сводку отчетов")]
    [ProducesResponseType(typeof(IReadOnlyList<ReportSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ReportSummaryResponse>>> GetSummaries(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        try
        {
            return Ok(await reportService.GetSummariesAsync(from, to, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный период", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpGet("{reportType}")]
    [EndpointSummary("Получить детализированный отчет")]
    [ProducesResponseType(typeof(ReportDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ReportDetailResponse>> GetDetails(
        string reportType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 30,
        CancellationToken ct = default)
    {
        try
        {
            var result = await reportService.GetDetailsAsync(reportType, from, to, skip, take, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректные параметры отчета", Detail = ex.Message, Status = 400 });
        }
    }

    [HttpGet("{reportType}/export.csv")]
    [EndpointSummary("Экспортировать отчет в CSV")]
    [Produces("text/csv")]
    [ProducesResponseType(typeof(byte[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportCsv(
        string reportType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        try
        {
            var csv = await reportService.ExportCsvAsync(reportType, from, to, ct);
            if (csv is null) return NotFound();
            return File(csv, "text/csv; charset=utf-8", $"itsm-{reportType}.csv");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Некорректный период", Detail = ex.Message, Status = 400 });
        }
    }
}
