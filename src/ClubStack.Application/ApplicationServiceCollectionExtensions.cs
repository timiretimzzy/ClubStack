using System.Reflection;
using ClubStack.Application.Authorization;
using ClubStack.Application.Cqrs;
using ClubStack.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace ClubStack.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<ISystemClock, SystemClock>();

        var assembly = typeof(ApplicationServiceCollectionExtensions).Assembly;

        services.ScanHandlers(assembly);

        _ = Permissions.All;

        return services;
    }

    private static void ScanHandlers(this IServiceCollection services, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            if (type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            foreach (var iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(ICommandHandler<,>))
                {
                    services.AddTransient(iface, type);
                }
                else if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IQueryHandler<,>))
                {
                    services.AddTransient(iface, type);
                }
            }
        }
    }
}