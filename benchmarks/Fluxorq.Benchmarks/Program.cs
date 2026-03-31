using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Fluxorq.Abstractions;
using Fluxorq.DependencyInjection;
using Mapture.Extensions.DependencyInjection;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).RunAll();

// ═══════════════════════════════════════════════════════════════════════════════
// Shared domain types
// ═══════════════════════════════════════════════════════════════════════════════

public class PersonEntity { public int Id { get; set; } public string FullName { get; set; } = ""; }
public class PersonDto   { public int Id { get; set; } public string FullName { get; set; } = ""; }

// ═══════════════════════════════════════════════════════════════════════════════
// Fluxorq request types, handler, and profile
// ═══════════════════════════════════════════════════════════════════════════════

public record FluxorqEchoRequest(string Payload)
    : Fluxorq.Abstractions.IRequest<string>;

public record FluxorqMapRequest(int Id, string FirstName, string LastName)
    : Fluxorq.Abstractions.IRequest<PersonDto>;

public class FluxorqEchoHandler
    : Fluxorq.Abstractions.IRequestHandler<FluxorqEchoRequest, string>
{
    public Task<string> HandleAsync(FluxorqEchoRequest req, CancellationToken ct = default)
        => Task.FromResult(req.Payload);
}

public class PersonProfile : Mapture.Profile
{
    public PersonProfile()
    {
        CreateMap<PersonEntity, PersonDto>();
    }
}

public class FluxorqMapRequestHandler
    : Fluxorq.Mapping.MappedRequestHandler<FluxorqMapRequest, PersonEntity, PersonDto>
{
    public FluxorqMapRequestHandler(Mapture.IMapper mapper) : base(mapper) { }

    protected override Task<PersonEntity> ResolveAsync(FluxorqMapRequest req, CancellationToken ct)
        => Task.FromResult(new PersonEntity { Id = req.Id, FullName = $"{req.FirstName} {req.LastName}" });
}

// ═══════════════════════════════════════════════════════════════════════════════
// MediatR request types and handler
// ═══════════════════════════════════════════════════════════════════════════════

public record MediatREchoRequest(string Payload)
    : MediatR.IRequest<string>;

public record MediatRMapRequest(int Id, string FirstName, string LastName)
    : MediatR.IRequest<PersonDto>;

public class MediatREchoHandler
    : MediatR.IRequestHandler<MediatREchoRequest, string>
{
    public Task<string> Handle(MediatREchoRequest req, CancellationToken ct)
        => Task.FromResult(req.Payload);
}

public class MediatRMapRequestHandler
    : MediatR.IRequestHandler<MediatRMapRequest, PersonDto>
{
    private readonly Mapture.IMapper _mapper;
    public MediatRMapRequestHandler(Mapture.IMapper mapper) => _mapper = mapper;

    public Task<PersonDto> Handle(MediatRMapRequest req, CancellationToken ct)
    {
        var entity = new PersonEntity { Id = req.Id, FullName = $"{req.FirstName} {req.LastName}" };
        return Task.FromResult(_mapper.Map<PersonEntity, PersonDto>(entity));
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Benchmark 1 — Pure dispatch, no pipeline behaviors
//   Measures the raw overhead of the dispatch mechanism.
// ═══════════════════════════════════════════════════════════════════════════════

[MemoryDiagnoser]
[SimpleJob]
[BenchmarkCategory("Dispatch_NoBehaviors")]
public class PureDispatchBenchmarks
{
    private Fluxorq.Abstractions.IDispatcher _fluxorq = null!;
    private MediatR.ISender _mediatr = null!;
    private FluxorqEchoRequest _fReq = null!;
    private MediatREchoRequest _mReq = null!;

    [GlobalSetup]
    public void Setup()
    {
        var fSvc = new ServiceCollection();
        fSvc.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        fSvc.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        fSvc.AddTransient<Fluxorq.Abstractions.IRequestHandler<FluxorqEchoRequest, string>, FluxorqEchoHandler>();
        fSvc.AddFluxorq(opts =>
        {
            opts.EnableLogging = false;
            opts.EnableValidation = false;
            opts.EnablePerformanceTracking = false;
        });
        _fluxorq = fSvc.BuildServiceProvider().GetRequiredService<Fluxorq.Abstractions.IDispatcher>();

        var mSvc = new ServiceCollection();
        mSvc.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        mSvc.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatREchoHandler).Assembly));
        _mediatr = mSvc.BuildServiceProvider().GetRequiredService<MediatR.ISender>();

        _fReq = new FluxorqEchoRequest("payload");
        _mReq = new MediatREchoRequest("payload");
    }

    [Benchmark(Baseline = true, Description = "Fluxorq — no behaviors")]
    public async Task<string> Fluxorq_NoBehaviors()
        => await _fluxorq.SendAsync(_fReq);

    [Benchmark(Description = "MediatR — no behaviors")]
    public async Task<string> MediatR_NoBehaviors()
        => await _mediatr.Send(_mReq);
}

// ═══════════════════════════════════════════════════════════════════════════════
// Benchmark 2 — Dispatch + object mapping
//   Both libraries use Mapture as the mapper for a fair comparison.
//   Fluxorq uses MappedRequestHandler; MediatR calls IMapper.Map manually.
// ═══════════════════════════════════════════════════════════════════════════════

[MemoryDiagnoser]
[SimpleJob]
[BenchmarkCategory("Dispatch_WithMapping")]
public class MappingDispatchBenchmarks
{
    private Fluxorq.Abstractions.IDispatcher _fluxorq = null!;
    private MediatR.ISender _mediatr = null!;
    private FluxorqMapRequest _fReq = null!;
    private MediatRMapRequest _mReq = null!;

    [GlobalSetup]
    public void Setup()
    {
        var fSvc = new ServiceCollection();
        fSvc.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        fSvc.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        fSvc.AddMapture(typeof(PersonProfile).Assembly);
        fSvc.AddTransient<Fluxorq.Abstractions.IRequestHandler<FluxorqMapRequest, PersonDto>, FluxorqMapRequestHandler>();
        fSvc.AddFluxorq(opts =>
        {
            opts.EnableLogging = false;
            opts.EnableValidation = false;
            opts.EnablePerformanceTracking = false;
        });
        _fluxorq = fSvc.BuildServiceProvider().GetRequiredService<Fluxorq.Abstractions.IDispatcher>();

        var mSvc = new ServiceCollection();
        mSvc.AddLogging(b => b.AddProvider(NullLoggerProvider.Instance));
        mSvc.AddMapture(typeof(PersonProfile).Assembly);
        mSvc.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRMapRequestHandler).Assembly));
        _mediatr = mSvc.BuildServiceProvider().GetRequiredService<MediatR.ISender>();

        _fReq = new FluxorqMapRequest(1, "Jane", "Smith");
        _mReq = new MediatRMapRequest(1, "Jane", "Smith");
    }

    [Benchmark(Baseline = true, Description = "Fluxorq — MappedRequestHandler")]
    public async Task<PersonDto> Fluxorq_WithMapping()
        => await _fluxorq.SendAsync(_fReq);

    [Benchmark(Description = "MediatR — manual IMapper.Map")]
    public async Task<PersonDto> MediatR_WithMapping()
        => await _mediatr.Send(_mReq);
}
