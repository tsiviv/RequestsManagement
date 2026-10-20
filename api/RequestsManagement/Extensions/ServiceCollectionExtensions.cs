using RequestsManagement.Services;

namespace RequestsManagement.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        // Scoped: one instance per HTTP request, so the header is parsed at most once per request.
        services.AddScoped<ICurrentUser, HeaderCurrentUser>();
        services.AddScoped<IRequestService, RequestService>();
        return services;
    }
}
