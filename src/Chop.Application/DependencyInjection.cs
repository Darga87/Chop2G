using Microsoft.Extensions.DependencyInjection;
using Chop.Application.Incidents;

namespace Chop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IIncidentService, IncidentService>();
        return services;
    }
}
