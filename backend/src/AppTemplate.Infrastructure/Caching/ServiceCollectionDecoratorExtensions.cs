namespace AppTemplate.Infrastructure.Caching;

/// <summary>Wraps an existing registration, for fail-open caches and invalidation decorators.</summary>
public static class ServiceCollectionDecoratorExtensions
{
    /// <summary>Wraps the last registration of <typeparamref name="TService"/>, keeping its lifetime.</summary>
    public static IServiceCollection Decorate<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService, TService> decorate
    )
        where TService : class
    {
        var descriptor = services.Last(d => d.ServiceType == typeof(TService) && !d.IsKeyedService);
        services.Remove(descriptor);
        services.Add(
            ServiceDescriptor.Describe(
                typeof(TService),
                provider => decorate(provider, (TService)Create(provider, descriptor)),
                descriptor.Lifetime
            )
        );
        return services;
    }

    private static object Create(IServiceProvider provider, ServiceDescriptor descriptor) =>
        descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(provider)
        ?? ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
}
