using Microsoft.AspNetCore.Mvc;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Services;

namespace RequestsManagement.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController(IRequestQueryService queries, IRequestStatusService statusUpdates) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<RequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<RequestDto>>> GetRequests(
        [FromQuery] RequestQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetRequestsAsync(query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(RequestSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await queries.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("filter-options")]
    [ProducesResponseType(typeof(RequestFilterOptionsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestFilterOptionsDto>> GetFilterOptions(CancellationToken cancellationToken)
    {
        var result = await queries.GetFilterOptionsAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestStatusHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RequestStatusHistoryDto>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        var result = await queries.GetHistoryAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(RequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RequestDto>> UpdateStatus(
        int id,
        [FromBody] UpdateRequestStatusDto dto,
        CancellationToken cancellationToken)
    {
        var result = await statusUpdates.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpPost("status/bulk")]
    [ProducesResponseType(typeof(BulkUpdateStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkUpdateStatusResultDto>> BulkUpdateStatus(
        [FromBody] BulkUpdateStatusDto dto,
        CancellationToken cancellationToken)
    {
        var result = await statusUpdates.BulkUpdateStatusAsync(dto, cancellationToken);
        return Ok(result);
    }
}
