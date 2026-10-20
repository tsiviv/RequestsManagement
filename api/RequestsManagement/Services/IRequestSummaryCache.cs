using RequestsManagement.DTOs;

namespace RequestsManagement.Services;

/// <summary>
/// Caches the request summary (aggregations over the whole table). The single seam to replace
/// with a distributed implementation when running more than one server instance.
/// </summary>
public interface IRequestSummaryCache
{
    Task<RequestSummaryDto> GetOrCreateAsync(Func<CancellationToken, Task<RequestSummaryDto>> factory, CancellationToken cancellationToken);
}
