using RequestsManagement.Services;

namespace RequestsManagement.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        // Scoped: one instance per HTTP request, so the header is parsed at most once per request.
        services.AddScoped<ICurrentUser, HeaderCurrentUser>();
        services.AddMemoryCache();
        // Singleton: the cache must outlive individual requests; it holds no scoped dependencies
        // (the DbContext-bound query is passed in by the scoped RequestService).
        services.AddSingleton<IRequestSummaryCache, MemoryRequestSummaryCache>();
        services.AddScoped<IRequestService, RequestService>();
        return services;
    }
}
