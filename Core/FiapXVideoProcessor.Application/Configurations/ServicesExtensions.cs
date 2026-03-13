using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace VideoProcessor.Application.Configurations;

public static class ServicesExtensions
{
    public static void ConfigureApplicationApp(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
    }
}