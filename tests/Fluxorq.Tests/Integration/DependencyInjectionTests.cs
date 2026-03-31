using Fluxorq.Abstractions;
using Fluxorq.Core;
using Fluxorq.DependencyInjection;
using Fluxorq.Tests.Helpers;
using Mapture.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fluxorq.Tests.Integration;

public class DependencyInjectionTests
{
    [Fact]
    public void AddFluxorq_RegistersDispatcher()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluxorq(opts => opts.ScanAssemblyContaining<PingHandler>());

        var sp = services.BuildServiceProvider();

        var dispatcher = sp.GetService<IDispatcher>();
        Assert.NotNull(dispatcher);
        Assert.IsType<Dispatcher>(dispatcher);
    }

    [Fact]
    public async Task AddFluxorq_ScansAndRegistersHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluxorq(opts =>
        {
            opts.ScanAssemblyContaining<PingHandler>();
            opts.EnableLogging = false;
            opts.EnableValidation = false;
            opts.EnablePerformanceTracking = false;
        });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var response = await dispatcher.SendAsync(new PingRequest("integration"));

        Assert.Equal("integration", response.Echo);
    }

    [Fact]
    public async Task AddFluxorq_ValidationEnabled_ValidatesRequests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMapture(typeof(UserProfile).Assembly);
        services.AddFluxorq(opts =>
        {
            opts.ScanAssemblyContaining<PingHandler>();
            opts.EnableLogging = false;
            opts.EnablePerformanceTracking = false;
            opts.EnableValidation = true;
        });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        await Assert.ThrowsAsync<Abstractions.Exceptions.FluxorqValidationException>(
            () => dispatcher.SendAsync(new PingRequest("")));
    }

    [Fact]
    public async Task AddFluxorq_MarkerTypeOverload_RegistersHandlers()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFluxorq<PingHandler>();

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IDispatcher>();

        var result = await dispatcher.SendAsync(new AddRequest(10, 5));
        Assert.Equal(15, result);
    }

    [Fact]
    public void AddFluxorq_CalledTwice_DoesNotDuplicateDispatcher()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddFluxorq(opts => opts.ScanAssemblyContaining<PingHandler>());
        services.AddFluxorq(opts => opts.ScanAssemblyContaining<PingHandler>());

        var sp = services.BuildServiceProvider();
        var dispatchers = sp.GetServices<IDispatcher>().ToList();

        // TryAddSingleton ensures only one Dispatcher is registered
        Assert.Single(dispatchers);
    }
}
