using RequestsManagement.DTOs;

namespace RequestsManagement.Services;

// The single seam to replace with a distributed implementation for more than one server instance.
public interface IRequestSummaryCache
{
    Task<RequestSummaryDto> GetOrCreateAsync(Func<CancellationToken, Task<RequestSummaryDto>> factory, CancellationToken cancellationToken);

    void Invalidate();
}
