using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;

namespace RequestsManagement.Services;

public interface IRequestService
{
    Task<PagedResultDto<RequestDto>> GetRequestsAsync(RequestQueryDto query, CancellationToken cancellationToken = default);

    Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<RequestDto> UpdateStatusAsync(int id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default);
}
