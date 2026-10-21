using RequestsManagement.DTOs;
using RequestsManagement.Exceptions;
using RequestsManagement.Models;

namespace RequestsManagement.Services;

internal static class RequestFilters
{
    public static void Validate(RequestQueryDto query)
    {
        if (query.CreatedFrom.HasValue && query.CreatedTo.HasValue && query.CreatedFrom.Value >= query.CreatedTo.Value)
        {
            throw new ValidationException("תאריך ההתחלה (createdFrom) חייב להיות מוקדם מתאריך הסיום (createdTo).");
        }
    }

    public static bool HasAny(RequestQueryDto query) =>
        !string.IsNullOrWhiteSpace(query.Search) ||
        query.Status.HasValue ||
        query.Priority.HasValue ||
        !string.IsNullOrWhiteSpace(query.OrganizationName) ||
        !string.IsNullOrWhiteSpace(query.AssignedTo) ||
        query.CreatedFrom.HasValue ||
        query.CreatedTo.HasValue;

    public static IQueryable<RequestEntity> ApplyFilters(this IQueryable<RequestEntity> requests, RequestQueryDto query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            requests = requests.Where(r => r.Title.Contains(search) || r.OrganizationName.Contains(search));
        }

        if (query.Status.HasValue)
        {
            requests = requests.Where(r => r.Status == query.Status.Value);
        }

        if (query.Priority.HasValue)
        {
            requests = requests.Where(r => r.Priority == query.Priority.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.OrganizationName))
        {
            var organizationName = query.OrganizationName.Trim();
            requests = requests.Where(r => r.OrganizationName == organizationName);
        }

        if (!string.IsNullOrWhiteSpace(query.AssignedTo))
        {
            var assignedTo = query.AssignedTo.Trim();
            requests = requests.Where(r => r.AssignedTo == assignedTo);
        }

        if (query.CreatedFrom.HasValue)
        {
            requests = requests.Where(r => r.CreatedAt >= query.CreatedFrom.Value);
        }

        if (query.CreatedTo.HasValue)
        {
            requests = requests.Where(r => r.CreatedAt < query.CreatedTo.Value);
        }

        return requests;
    }
}
