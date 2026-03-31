using Fluxorq.Abstractions;
using Fluxorq.Abstractions.Exceptions;
using Fluxorq.Core;
using Fluxorq.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxorq.Tests.Core;

public class DispatcherTests
{
    private static IServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SendAsync_ReturnsHandlerResponse()
    {
        var sp = BuildProvider(s => s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>());
        var dispatcher = new Dispatcher(sp);

        var response = await dispatcher.SendAsync(new PingRequest("hello"));

        Assert.Equal("hello", response.Echo);
    }

    [Fact]
    public async Task SendAsync_ValueType_ReturnsHandlerResponse()
    {
        var sp = BuildProvider(s => s.AddTransient<IRequestHandler<AddRequest, int>, AddHandler>());
        var dispatcher = new Dispatcher(sp);

        var result = await dispatcher.SendAsync(new AddRequest(3, 4));

        Assert.Equal(7, result);
    }

    [Fact]
    public async Task SendAsync_CachesInvoker_SameTypeDispatchedTwice()
    {
        var sp = BuildProvider(s => s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>());
        var dispatcher = new Dispatcher(sp);

        // First call builds and caches the invoker; second call uses the cache
        var r1 = await dispatcher.SendAsync(new PingRequest("first"));
        var r2 = await dispatcher.SendAsync(new PingRequest("second"));

        Assert.Equal("first", r1.Echo);
        Assert.Equal("second", r2.Echo);
    }

    [Fact]
    public async Task SendAsync_NullRequest_ThrowsArgumentNullException()
    {
        var sp = BuildProvider(_ => { });
        var dispatcher = new Dispatcher(sp);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => dispatcher.SendAsync<PingResponse>(null!));
    }

    [Fact]
    public async Task SendAsync_NoHandlerRegistered_ThrowsHandlerNotFoundException()
    {
        var sp = BuildProvider(_ => { });
        var dispatcher = new Dispatcher(sp);

        await Assert.ThrowsAsync<HandlerNotFoundException>(
            () => dispatcher.SendAsync(new PingRequest("x")));
    }

    [Fact]
    public async Task SendAsync_HandlerThrows_PropagatesException()
    {
        var sp = BuildProvider(s => s.AddTransient<IRequestHandler<PingRequest, PingResponse>, FaultingHandler>());
        var dispatcher = new Dispatcher(sp);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new PingRequest("boom")));
    }

    [Fact]
    public async Task SendAsync_CancellationRequested_IsPropagated()
    {
        var sp = BuildProvider(s => s.AddTransient<IRequestHandler<PingRequest, PingResponse>, CancellingHandler>());
        var dispatcher = new Dispatcher(sp);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => dispatcher.SendAsync(new PingRequest("cancel"), cts.Token));
    }
}
