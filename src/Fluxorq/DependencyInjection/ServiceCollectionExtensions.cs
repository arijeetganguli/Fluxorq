using System.Reflection;
using Fluxorq.Abstractions;
using Fluxorq.Core;
using Fluxorq.DependencyInjection.Internal;
using Fluxorq.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fluxorq.DependencyInjection;

/// <summary>
/// Extension methods for registering Fluxorq services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Fluxorq: the dispatcher, pipeline behaviors, handlers, and validators
    /// found in the provided assemblies.
    ///
    /// <example>
    /// <code>
    /// // Scan the assembly that contains Program
    /// services.AddFluxorq(opts => opts.ScanAssemblyContaining&lt;Program&gt;());
    ///
    /// // Opt-out of specific behaviors
    /// services.AddFluxorq(opts =>
    /// {
    ///     opts.ScanAssemblyContaining&lt;Program&gt;();
    ///     opts.EnablePerformanceTracking = false;
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public static IServiceCollection AddFluxorq(
        this IServiceCollection services,
        Action<FluxorqOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new FluxorqOptions();
        configure?.Invoke(options);

        services.TryAddSingleton<IDispatcher, Dispatcher>();

        if (options.EnableLogging)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));

        if (options.EnableValidation)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        if (options.EnablePerformanceTracking)
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));

        HandlerRegistrar.Register(services, options.AssembliesToScan);

        return services;
    }

    /// <summary>
    /// Convenience overload: scans the assembly containing <typeparamref name="TMarker"/>
    /// with all default behaviors enabled.
    /// </summary>
    public static IServiceCollection AddFluxorq<TMarker>(this IServiceCollection services)
        => services.AddFluxorq(opts => opts.ScanAssemblyContaining<TMarker>());

    /// <summary>
    /// Convenience overload: scans the provided assembly with all default behaviors enabled.
    /// </summary>
    public static IServiceCollection AddFluxorq(this IServiceCollection services, Assembly assembly)
        => services.AddFluxorq(opts => opts.ScanAssemblies(assembly));
}
