using System.Linq.Expressions;
using RequestsManagement.DTOs;
using RequestsManagement.Exceptions;
using RequestsManagement.Models;

namespace RequestsManagement.Services;

internal static class RequestSorting
{
    private static readonly string[] SortableFields =
        ["createdat", "updatedat", "organizationname", "priority", "id"];

    public static (string SortBy, bool Descending) Resolve(RequestQueryDto query)
    {
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy) ? RequestQueryDto.DefaultSortBy : query.SortBy.Trim();
        var sortDirection = string.IsNullOrWhiteSpace(query.SortDirection) ? RequestQueryDto.DefaultSortDirection : query.SortDirection.Trim();

        if (!SortableFields.Contains(sortBy.ToLowerInvariant()))
        {
            throw new ValidationException(
                $"שדה המיון '{sortBy}' אינו נתמך. ערכים מותרים: {string.Join(", ", SortableFields)}.");
        }

        if (!string.Equals(sortDirection, "asc", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"כיוון המיון '{sortDirection}' אינו נתמך. ערכים מותרים: asc, desc.");
        }

        return (sortBy, string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase));
    }

    // Each order matches a supporting index key exactly and ends with Id (stable paging, no Sort);
    // that is why organization and priority sort by CreatedAt second.
    public static IQueryable<RequestEntity> ApplySort(this IQueryable<RequestEntity> query, string sortBy, bool descending) =>
        sortBy.ToLowerInvariant() switch
        {
            "id" => OrderBy(query, r => r.Id, descending),
            "updatedat" => ThenBy(OrderBy(query, r => r.UpdatedAt, descending), r => r.Id, descending),
            "organizationname" => ThenBy(ThenBy(OrderBy(query, r => r.OrganizationName, descending), r => r.CreatedAt, descending), r => r.Id, descending),
            "priority" => ThenBy(ThenBy(OrderBy(query, r => r.Priority, descending), r => r.CreatedAt, descending), r => r.Id, descending),
            _ => ThenBy(OrderBy(query, r => r.CreatedAt, descending), r => r.Id, descending)
        };

    private static IOrderedQueryable<RequestEntity> OrderBy<TKey>(
        IQueryable<RequestEntity> query, Expression<Func<RequestEntity, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    private static IOrderedQueryable<RequestEntity> ThenBy<TKey>(
        IOrderedQueryable<RequestEntity> query, Expression<Func<RequestEntity, TKey>> key, bool descending) =>
        descending ? query.ThenByDescending(key) : query.ThenBy(key);
}
