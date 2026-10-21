using RequestsManagement.DTOs;

namespace RequestsManagement.Services;

public interface IRequestStatusService
{
    Task<RequestDto> UpdateStatusAsync(int id, UpdateRequestStatusDto dto, CancellationToken cancellationToken = default);

    Task<BulkUpdateStatusResultDto> BulkUpdateStatusAsync(BulkUpdateStatusDto dto, CancellationToken cancellationToken = default);
}
