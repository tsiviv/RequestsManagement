using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;

namespace RequestsManagement.Services.Interfaces;

public interface IRequestService
{
    Task<PagedResultDto<RequestDto>> GetRequestsAsync(RequestQueryDto query, CancellationToken cancellationToken = default);

    Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<RequestDto> UpdateStatusAsync(int id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default);
}
