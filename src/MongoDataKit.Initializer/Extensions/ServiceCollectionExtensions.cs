using Microsoft.Extensions.DependencyInjection;
using MongoDataKit.Core.Interfaces;

namespace MongoDataKit.Initializer.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the database initializer service.
    /// </summary>
    public static IServiceCollection AddMongoInitializer(this IServiceCollection services)
    {
        services.AddSingleton<IDatabaseInitializer, DatabaseInitializer>();
        return services;
    }

    /// <summary>
    /// Registers an audit context for tracking user identity in audit fields.
    /// </summary>
    /// <typeparam name="TAuditContext">The audit context implementation type.</typeparam>
    public static IServiceCollection AddAuditContext<TAuditContext>(this IServiceCollection services)
        where TAuditContext : class, IAuditContext
    {
        services.AddScoped<IAuditContext, TAuditContext>();
        return services;
    }

    /// <summary>
    /// Registers the anonymous audit context (no user tracking).
    /// </summary>
    public static IServiceCollection AddAnonymousAuditContext(this IServiceCollection services)
    {
        services.AddSingleton<IAuditContext, AnonymousAuditContext>();
        return services;
    }
}
