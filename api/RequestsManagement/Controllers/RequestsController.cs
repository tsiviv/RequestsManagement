using Microsoft.AspNetCore.Mvc;
using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;
using RequestsManagement.Services;

namespace RequestsManagement.Controllers;

[ApiController]
[Route("api/requests")]
public class RequestsController(IRequestService requestService) : ControllerBase
{
    /// <summary>Returns a filtered, sorted, paged list of requests.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<RequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<RequestDto>>> GetRequests(
        [FromQuery] RequestQueryDto query,
        CancellationToken cancellationToken)
    {
        var result = await requestService.GetRequestsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns aggregate counts of requests by status and priority.</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(RequestSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RequestSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var result = await requestService.GetSummaryAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>Returns the status change history of a request, newest first.</summary>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<RequestStatusHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RequestStatusHistoryDto>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        var result = await requestService.GetHistoryAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Updates a request's status using optimistic concurrency via RowVersion.</summary>
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
        var result = await requestService.UpdateStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the status of up to 100 requests with partial success: returns 200 with a per-request
    /// outcome (Updated / NotFound / Conflict / InvalidTransition); 400 only for request-level errors.
    /// </summary>
    [HttpPost("status/bulk")]
    [ProducesResponseType(typeof(BulkUpdateStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BulkUpdateStatusResultDto>> BulkUpdateStatus(
        [FromBody] BulkUpdateStatusDto dto,
        CancellationToken cancellationToken)
    {
        var result = await requestService.BulkUpdateStatusAsync(dto, cancellationToken);
        return Ok(result);
    }
}
