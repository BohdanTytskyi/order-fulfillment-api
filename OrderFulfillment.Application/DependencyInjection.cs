using System.Reflection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderFulfillment.Application.Common.Behaviors;

namespace OrderFulfillment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(DistributedLockBehavior<,>));
        });
        
        return services;
    }
}