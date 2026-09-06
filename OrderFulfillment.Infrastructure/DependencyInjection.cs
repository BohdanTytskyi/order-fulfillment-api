using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderFulfillment.Application.Interfaces;
using OrderFulfillment.Infrastructure.BackgroundJobs;
using OrderFulfillment.Infrastructure.Data;
using OrderFulfillment.Infrastructure.Repositories;
using OrderFulfillment.Infrastructure.Services;
using StackExchange.Redis;

namespace OrderFulfillment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' not found.");
        }

        services.AddDbContext<ApplicationDbContext>((DbContextOptionsBuilder options) =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        string redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>((IServiceProvider provider) =>
            ConnectionMultiplexer.Connect(redisConnection));

        services.AddScoped<IDistributedLockService, RedisDistributedLockService>();

        services.AddHostedService<OutboxProcessorBackgroundService>();

        return services;
    }
}