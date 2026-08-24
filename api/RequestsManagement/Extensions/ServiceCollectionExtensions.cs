using RequestsManagement.Services;
using RequestsManagement.Services.Interfaces;

namespace RequestsManagement.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IRequestService, RequestService>();
        return services;
    }
}
