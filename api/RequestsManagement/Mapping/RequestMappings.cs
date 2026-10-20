using System.Linq.Expressions;
using RequestsManagement.DTOs;
using RequestsManagement.Models;
using RequestsManagement.Policies;

namespace RequestsManagement.Mapping;

public static class RequestMappings
{
    // Single source of truth for RequestEntity -> RequestDto. As an expression it can be passed
    // to IQueryable.Select, so EF reads only the mapped columns instead of materializing entities.
    public static readonly Expression<Func<RequestEntity, RequestDto>> ToDtoProjection = r => new RequestDto
    {
        Id = r.Id,
        Title = r.Title,
        OrganizationName = r.OrganizationName,
        AssignedTo = r.AssignedTo,
        Status = r.Status,
        Priority = r.Priority,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        // Evaluated in memory by EF (allowed in the final projection), from the Status column it
        // already reads; adds no SQL.
        AllowedNextStatuses = StatusTransitionPolicy.GetAllowedNextStatuses(r.Status),
        RowVersion = Convert.ToBase64String(r.RowVersion)
    };

    // Compiled once, for entities already in memory (e.g. after an update).
    private static readonly Func<RequestEntity, RequestDto> ToDtoCompiled = ToDtoProjection.Compile();

    public static RequestDto ToDto(this RequestEntity entity) => ToDtoCompiled(entity);

    public static readonly Expression<Func<RequestStatusHistoryEntity, RequestStatusHistoryDto>> ToHistoryDtoProjection = h => new RequestStatusHistoryDto
    {
        Id = h.Id,
        PreviousStatus = h.PreviousStatus,
        NewStatus = h.NewStatus,
        ChangedAt = h.ChangedAt,
        ChangedBy = h.ChangedBy
    };
}
