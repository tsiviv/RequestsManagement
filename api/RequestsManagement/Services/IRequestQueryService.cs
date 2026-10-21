using RequestsManagement.DTOs;
using RequestsManagement.DTOs.Common;

namespace RequestsManagement.Services;

public interface IRequestQueryService
{
    Task<PagedResultDto<RequestDto>> GetRequestsAsync(RequestQueryDto query, CancellationToken cancellationToken = default);

    Task<RequestSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<RequestFilterOptionsDto> GetFilterOptionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequestStatusHistoryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken = default);
}
