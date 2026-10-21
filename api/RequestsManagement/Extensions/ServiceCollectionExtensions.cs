using RequestsManagement.Services;

namespace RequestsManagement.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HeaderCurrentUser>();
        services.AddMemoryCache();
        services.AddSingleton<IRequestSummaryCache, MemoryRequestSummaryCache>();
        services.AddScoped<IRequestQueryService, RequestQueryService>();
        services.AddScoped<IRequestStatusService, RequestStatusService>();
        return services;
    }
}
