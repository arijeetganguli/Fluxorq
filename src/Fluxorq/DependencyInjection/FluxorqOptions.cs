using System.Reflection;
using Fluxorq.Pipeline;

namespace Fluxorq.DependencyInjection;

/// <summary>
/// Configuration options for Fluxorq passed to <c>AddFluxorq()</c>.
/// Controls which assemblies are scanned for handlers and which built-in
/// pipeline behaviors are registered.
/// </summary>
public sealed class FluxorqOptions
{
    private readonly List<Assembly> _assembliesToScan = [];

    /// <summary>The assemblies that will be scanned for <c>IRequestHandler</c> and <c>IValidator</c> implementations.</summary>
    public IReadOnlyList<Assembly> AssembliesToScan => _assembliesToScan;

    /// <summary>
    /// Register <see cref="LoggingBehavior{TRequest,TResponse}"/> in the pipeline.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Register <see cref="ValidationBehavior{TRequest,TResponse}"/> in the pipeline.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnableValidation { get; set; } = true;

    /// <summary>
    /// Register <see cref="PerformanceBehavior{TRequest,TResponse}"/> in the pipeline.
    /// Default: <c>true</c>.
    /// </summary>
    public bool EnablePerformanceTracking { get; set; } = true;

    /// <summary>
    /// Adds the specified assemblies to the scan list.
    /// </summary>
    public FluxorqOptions ScanAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        _assembliesToScan.AddRange(assemblies);
        return this;
    }

    /// <summary>
    /// Adds the assembly that contains <typeparamref name="TMarker"/> to the scan list.
    /// </summary>
    public FluxorqOptions ScanAssemblyContaining<TMarker>()
    {
        _assembliesToScan.Add(typeof(TMarker).Assembly);
        return this;
    }
}
