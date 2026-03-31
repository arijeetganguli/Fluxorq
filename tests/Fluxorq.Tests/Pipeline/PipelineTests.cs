using Fluxorq.Abstractions;
using Fluxorq.Abstractions.Exceptions;
using Fluxorq.Core;
using Fluxorq.Pipeline;
using Fluxorq.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Fluxorq.Tests.Pipeline;

public class LoggingBehaviorTests
{
    private static IServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task LoggingBehavior_PassesResponseThrough()
    {
        var sp = BuildProvider(s =>
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>());
        var dispatcher = new Dispatcher(sp);

        var response = await dispatcher.SendAsync(new PingRequest("log-test"));

        Assert.Equal("log-test", response.Echo);
    }

    [Fact]
    public async Task LoggingBehavior_PropagatesHandlerException()
    {
        var sp = BuildProvider(s =>
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, FaultingHandler>());
        var dispatcher = new Dispatcher(sp);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new PingRequest("fail")));
    }
}

public class ValidationBehaviorTests
{
    private static IServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ValidationBehavior_NoValidators_PassesThrough()
    {
        var sp = BuildProvider(s =>
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>());
        var dispatcher = new Dispatcher(sp);

        var response = await dispatcher.SendAsync(new PingRequest("valid"));

        Assert.Equal("valid", response.Echo);
    }

    [Fact]
    public async Task ValidationBehavior_ValidRequest_PassesThrough()
    {
        var sp = BuildProvider(s =>
        {
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>();
            s.AddTransient<IValidator<PingRequest>, PingRequestValidator>();
        });
        var dispatcher = new Dispatcher(sp);

        var response = await dispatcher.SendAsync(new PingRequest("hello"));

        Assert.Equal("hello", response.Echo);
    }

    [Fact]
    public async Task ValidationBehavior_InvalidRequest_ThrowsValidationException()
    {
        var sp = BuildProvider(s =>
        {
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>();
            s.AddTransient<IValidator<PingRequest>, PingRequestValidator>();
        });
        var dispatcher = new Dispatcher(sp);

        var ex = await Assert.ThrowsAsync<FluxorqValidationException>(
            () => dispatcher.SendAsync(new PingRequest("")));

        Assert.Single(ex.Failures);
        Assert.Equal("Message", ex.Failures[0].PropertyName);
    }

    [Fact]
    public async Task ValidationBehavior_MultipleFailures_AllReported()
    {
        var sp = BuildProvider(s =>
        {
            s.AddTransient<IRequestHandler<AddRequest, int>, AddHandler>();
            s.AddTransient<IValidator<AddRequest>, AddRequestValidator>();
        });
        var dispatcher = new Dispatcher(sp);

        var ex = await Assert.ThrowsAsync<FluxorqValidationException>(
            () => dispatcher.SendAsync(new AddRequest(-1, -2)));

        Assert.Equal(2, ex.Failures.Count);
    }

    [Fact]
    public async Task ValidationBehavior_ShortCircuits_HandlerNotInvoked()
    {
        bool handlerInvoked = false;

        var sp = BuildProvider(s =>
        {
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>>(_ =>
                new TrackingHandler(() => { handlerInvoked = true; }));
            s.AddTransient<IValidator<PingRequest>, PingRequestValidator>();
        });
        var dispatcher = new Dispatcher(sp);

        await Assert.ThrowsAsync<FluxorqValidationException>(
            () => dispatcher.SendAsync(new PingRequest("")));

        Assert.False(handlerInvoked);
    }

    // Helper handler that records whether it was invoked
    private sealed class TrackingHandler : IRequestHandler<PingRequest, PingResponse>
    {
        private readonly Action _onInvoke;
        public TrackingHandler(Action onInvoke) => _onInvoke = onInvoke;
        public Task<PingResponse> HandleAsync(PingRequest request, CancellationToken ct = default)
        {
            _onInvoke();
            return Task.FromResult(new PingResponse(request.Message));
        }
    }
}

public class PerformanceBehaviorTests
{
    private static IServiceProvider BuildProvider(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        configure(services);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PerformanceBehavior<,>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task PerformanceBehavior_PassesResponseThrough()
    {
        var sp = BuildProvider(s =>
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>());
        var dispatcher = new Dispatcher(sp);

        var response = await dispatcher.SendAsync(new PingRequest("perf"));

        Assert.Equal("perf", response.Echo);
    }

    [Fact]
    public async Task PerformanceBehavior_PropagatesHandlerException()
    {
        var sp = BuildProvider(s =>
            s.AddTransient<IRequestHandler<PingRequest, PingResponse>, FaultingHandler>());
        var dispatcher = new Dispatcher(sp);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => dispatcher.SendAsync(new PingRequest("boom")));
    }
}

public class PipelineOrderTests
{
    [Fact]
    public async Task Behaviors_ExecuteIn_RegistrationOrder()
    {
        var order = new List<string>();

        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<PingRequest, PingResponse>, PingHandler>();
        services.AddTransient<IPipelineBehavior<PingRequest, PingResponse>>(
            _ => new RecordingBehavior<PingRequest, PingResponse>("first", order));
        services.AddTransient<IPipelineBehavior<PingRequest, PingResponse>>(
            _ => new RecordingBehavior<PingRequest, PingResponse>("second", order));

        var dispatcher = new Dispatcher(services.BuildServiceProvider());

        await dispatcher.SendAsync(new PingRequest("order"));

        Assert.Equal(["first", "second"], order);
    }

    [Fact]
    public async Task Behavior_CanShortCircuit_WithoutCallingNext()
    {
        bool handlerInvoked = false;

        var services = new ServiceCollection();
        services.AddTransient<IRequestHandler<PingRequest, PingResponse>>(
            _ => new TrackingHandler2(() => handlerInvoked = true));
        services.AddTransient<IPipelineBehavior<PingRequest, PingResponse>>(
            _ => new ShortCircuitBehavior());

        var dispatcher = new Dispatcher(services.BuildServiceProvider());

        var response = await dispatcher.SendAsync(new PingRequest("block"));

        Assert.Equal("short-circuited", response.Echo);
        Assert.False(handlerInvoked);
    }

    private sealed class RecordingBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
        where TReq : IRequest<TRes>
    {
        private readonly string _name;
        private readonly List<string> _log;
        public RecordingBehavior(string name, List<string> log) { _name = name; _log = log; }

        public async Task<TRes> HandleAsync(TReq request, NextDelegate<TRes> next, CancellationToken ct = default)
        {
            _log.Add(_name);
            return await next();
        }
    }

    private sealed class ShortCircuitBehavior : IPipelineBehavior<PingRequest, PingResponse>
    {
        public Task<PingResponse> HandleAsync(PingRequest request, NextDelegate<PingResponse> next, CancellationToken ct = default)
            => Task.FromResult(new PingResponse("short-circuited"));
    }

    private sealed class TrackingHandler2 : IRequestHandler<PingRequest, PingResponse>
    {
        private readonly Action _onInvoke;
        public TrackingHandler2(Action onInvoke) => _onInvoke = onInvoke;
        public Task<PingResponse> HandleAsync(PingRequest request, CancellationToken ct = default)
        {
            _onInvoke();
            return Task.FromResult(new PingResponse(request.Message));
        }
    }
}
