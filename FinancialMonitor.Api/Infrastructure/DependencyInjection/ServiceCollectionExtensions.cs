using FinancialMonitor.Api.Application.Abstractions;
using FinancialMonitor.Api.Infrastructure.Caching;
using FinancialMonitor.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace FinancialMonitor.Api.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTransactionCaching(
        this IServiceCollection services,
        string databaseProvider,
        IConfiguration configuration)
    {
        switch (databaseProvider)
        {
            case "postgres":
                var redisConnectionString = configuration.GetConnectionString("Redis")
                    ?? throw new InvalidOperationException(
                        "ConnectionStrings:Redis is required when Database:Provider is Postgres.");
                var redis = ConnectionMultiplexer.Connect(redisConnectionString);
                services.AddSingleton<IConnectionMultiplexer>(redis);
                services.AddStackExchangeRedisCache(options =>
                {
                    options.Configuration = redisConnectionString;
                    options.InstanceName = "FinancialMonitor:";
                });
                services.AddSingleton<ITransactionCache, RedisTransactionCache>();
                break;

            case "sqlite":
                services.AddMemoryCache();
                services.AddSingleton<ITransactionCache, MemoryTransactionCache>();
                break;

            default:
                throw new InvalidOperationException("Database:Provider must be Sqlite or Postgres.");
        }

        services.AddKeyedSingleton<ITransactionRepository>(
            "inner",
            (serviceProvider, _) => serviceProvider.GetRequiredService<EfTransactionRepository>());
        services.AddSingleton<ITransactionRepository, CachedTransactionRepository>();

        return services;
    }
}