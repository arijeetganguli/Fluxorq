# Fluxorq

[![NuGet](https://img.shields.io/nuget/v/Fluxorq.svg)](https://www.nuget.org/packages/Fluxorq)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://github.com/arijeetganguli/Fluxorq/actions/workflows/ci.yml/badge.svg)](https://github.com/arijeetganguli/Fluxorq/actions)

> High-performance request dispatching and pipeline framework for .NET — with first-class object mapping powered by [Mapture](https://github.com/arijeetganguli/Mapture).

Fluxorq is an original implementation of the mediator and middleware patterns designed for production .NET applications. It gives you a familiar developer experience if you are coming from MediatR, while eliminating the need for AutoMapper through tight integration with Mapture.

---

## Why Fluxorq?

| Concern | Without Fluxorq | With Fluxorq |
|---|---|---|
| Request routing | Manual service resolution or static factories | `IDispatcher.SendAsync(request)` |
| Cross-cutting concerns | Scattered try/catch, if/else | Composable `IPipelineBehavior<,>` |
| Object mapping | AutoMapper config sprawl | Zero-config `entity.MapTo<Dto>(mapper)` |
| Boilerplate handlers | Repeated Resolve → Map pattern | `MappedRequestHandler<TReq, TEntity, TDto>` |
| DI registration | Manual `AddTransient` for every handler | `services.AddFluxorq(opts => ...)` |

---

## Installation

```bash
dotnet add package Fluxorq
dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

---

## Quick Start

### 1. Define a request and response

```csharp
using Fluxorq.Abstractions;

public record GetProductRequest(Guid Id) : IRequest<ProductDto>;

public class ProductDto
{
    public Guid Id  { get; set; }
    public string Name  { get; set; } = "";
    public bool InStock { get; set; }
}
```

### 2. Implement a handler

```csharp
// Option A — plain handler
public class GetProductHandler : IRequestHandler<GetProductRequest, ProductDto>
{
    private readonly IProductRepository _repo;
    private readonly IMapper _mapper;

    public GetProductHandler(IProductRepository repo, IMapper mapper)
    {
        _repo   = repo;
        _mapper = mapper;
    }

    public async Task<ProductDto> HandleAsync(GetProductRequest req, CancellationToken ct = default)
    {
        var product = await _repo.GetByIdAsync(req.Id, ct)
            ?? throw new KeyNotFoundException($"Product {req.Id} not found.");
        return _mapper.Map<Product, ProductDto>(product);
    }
}

// Option B — MappedRequestHandler (recommended for Resolve → Map patterns)
public class GetProductHandler : MappedRequestHandler<GetProductRequest, Product, ProductDto>
{
    private readonly IProductRepository _repo;

    public GetProductHandler(IProductRepository repo, IMapper mapper) : base(mapper)
        => _repo = repo;

    protected override async Task<Product> ResolveAsync(GetProductRequest req, CancellationToken ct)
        => await _repo.GetByIdAsync(req.Id, ct)
           ?? throw new KeyNotFoundException($"Product {req.Id} not found.");
}
```

### 3. Define a Mapture profile

```csharp
using Mapture;

public class ProductProfile : Profile
{
    public ProductProfile()
    {
        CreateMap<Product, ProductDto>()
            .ForMember(d => d.InStock,
                opt => opt.MapFrom((Func<Product, bool>)(s => s.StockQuantity > 0)));
    }
}
```

### 4. Register with DI

```csharp
using Fluxorq.DependencyInjection;
using Mapture.Extensions.DependencyInjection;

builder.Services.AddMapture(typeof(Program).Assembly);
builder.Services.AddFluxorq(opts => opts.ScanAssemblyContaining<Program>());
```

### 5. Dispatch

```csharp
public class ProductsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public ProductsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.SendAsync(new GetProductRequest(id), ct);
        return Ok(dto);
    }
}
```

---

## Pipeline Behaviors

Behaviors run as ordered middleware around every request. Register them before or after the built-in ones.

### Built-in behaviors (registered by `AddFluxorq` in this order)

| Behavior | Purpose |
|---|---|
| `LoggingBehavior<,>` | Logs request entry, success, and error |
| `ValidationBehavior<,>` | Runs all `IValidator<TRequest>` and throws `FluxorqValidationException` |
| `PerformanceBehavior<,>` | Logs a warning when a request exceeds 500 ms |

### Opting out

```csharp
services.AddFluxorq(opts =>
{
    opts.ScanAssemblyContaining<Program>();
    opts.EnableLogging = false;
    opts.EnablePerformanceTracking = false;
    // opts.EnableValidation = true  (default)
});
```

### Writing a custom behavior

```csharp
public class CachingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IMemoryCache _cache;

    public CachingBehavior(IMemoryCache cache) => _cache = cache;

    public async Task<TResponse> HandleAsync(
        TRequest request,
        NextDelegate<TResponse> next,
        CancellationToken ct = default)
    {
        var key = $"fluxorq:{typeof(TRequest).Name}";
        if (_cache.TryGetValue(key, out TResponse? cached))
            return cached!;

        var result = await next();
        _cache.Set(key, result, TimeSpan.FromMinutes(5));
        return result;
    }
}

// Register it
services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
```

---

## Validation

Implement `IValidator<TRequest>` and register it. `ValidationBehavior` picks it up automatically.

```csharp
public class CreateProductValidator : IValidator<CreateProductRequest>
{
    public ValidationResult Validate(CreateProductRequest req)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(req.Name))
            failures.Add(new ValidationFailure(nameof(req.Name), "Name is required."));

        if (req.Price <= 0)
            failures.Add(new ValidationFailure(nameof(req.Price), "Price must be positive."));

        return failures.Count > 0
            ? ValidationResult.Fail(failures)
            : ValidationResult.Success;
    }
}
```

Catch `FluxorqValidationException` to inspect failures:

```csharp
try
{
    await dispatcher.SendAsync(new CreateProductRequest("", -1m, 0));
}
catch (FluxorqValidationException ex)
{
    foreach (var f in ex.Failures)
        Console.WriteLine($"{f.PropertyName}: {f.ErrorMessage}");
}
```

---

## Mapping

Fluxorq uses Mapture for object mapping. The `MapTo<TDestination>()` extension provides a fluent, compiled-delegate-backed mapping call.

```csharp
// Requires IMapper injected
var dto = entity.MapTo<ProductDto>(mapper);
```

The compiled delegate is cached per source/destination type pair — subsequent calls have near-zero overhead.

---

## Object Structure

```
Fluxorq/
├── src/
│   └── Fluxorq/
│       ├── Abstractions/          IRequest, IRequestHandler, IDispatcher, IPipelineBehavior, IValidator,
│       │                          ValidationResult, ValidationFailure, NextDelegate, exceptions
│       ├── Core/                  Dispatcher, SendInvoker (compiled-delegate cache)
│       ├── Pipeline/              LoggingBehavior, ValidationBehavior, PerformanceBehavior
│       ├── Mapping/               MappingExtensions (MapTo<>), MappedRequestHandler
│       └── DependencyInjection/   ServiceCollectionExtensions (AddFluxorq), FluxorqOptions
├── tests/
│   └── Fluxorq.Tests/             29 unit + integration tests
├── samples/
│   └── Fluxorq.Sample/            End-to-end product CRUD example
├── benchmarks/
│   └── Fluxorq.Benchmarks/        BenchmarkDotNet benchmarks
└── docs/
    ├── MigrationFromMediatR.md
    └── ReplacingAutoMapper.md
```

---

## Performance

Fluxorq is designed so that the dispatch mechanism never becomes a bottleneck in a real application.

| Design decision | Effect |
|---|---|
| **Typed invoker cache** (`ConcurrentDictionary<Type, object>`) | First call per request type compiles a strongly-typed `RequestInvokerImpl<TReq,TRes>`. Every subsequent call is a lock-free dictionary lookup — zero reflection on the hot path. |
| **`RequestInvoker<TResponse>` abstract base** | Generic on `TResponse` only; stored as `object` in the cache, cast back in `Dispatcher`. Eliminates all boxing of the response — `TResponse` flows through without ever being widened to `object`. |
| **Non-async `Dispatcher` + invoker** | Neither `Dispatcher.SendAsync` nor `RequestInvokerImpl.InvokeAsync` carries the `async` keyword. The `Task<TResponse>` is returned directly — zero state-machine allocations on the dispatch path itself. |
| **`PipelineRunner` index walker** | One object + one bound `NextDelegate` per dispatch, regardless of behavior count. Previous closure-chain approach allocated N+1 heap objects per request. |
| **Zero-behavior fast path** | When no `IPipelineBehavior<,>` are registered, the handler is called directly — no pipeline runner constructed at all. |
| **`ValidationBehavior` imperative loop** | No LINQ iterators on the hot path. The failure `List<>` is only allocated when a validator actually reports a failure. |
| **Static service type fields** | `typeof(IRequestHandler<TReq,TRes>)` is stored as `static readonly` on the generic class — one field load instead of a type-token instruction per call. |

Run the benchmarks yourself:

```bash
dotnet run --project benchmarks/Fluxorq.Benchmarks -c Release
```

---

## Benchmark Results

> Environment: BenchmarkDotNet v0.15.8 · .NET 10.0.3 (RyuJIT x64) · Windows 11  
> CPU: Intel Core Ultra 7 265H 2.20 GHz · 16 cores  
> GC mode: Concurrent Workstation  
> Measured in Release mode with no pipeline behaviors active

### Scenario 1 — Pure dispatch (no behaviors active)

Both libraries dispatch a single `IRequest<string>` to an echo handler with no middleware in the chain.

| | Fluxorq | MediatR 14 | Δ |
|---|---:|---:|---:|
| **Mean latency** | **74 ns** | 90 ns | **−18% faster** |
| **Allocated** | **168 B** | 272 B | **−38% less** |
| Gen0 collections | 0.0134 | 0.0216 | — |

```
Latency (ns)       0          50         100
               ─────────────────────────────
Fluxorq        ██████████████░░░░░░░  74 ns  ✓ faster
MediatR        █████████████████░░░░  90 ns
```

```
Heap allocated (bytes)    0     100     200     300
                     ─────────────────────────────────
Fluxorq              █████████░░░░░░░░░░░░  168 B  ✓ less
MediatR              ████████████████░░░░░  272 B
```

### Scenario 2 — Dispatch + object mapping

Both use Mapture as the mapper. Fluxorq uses `MappedRequestHandler`; MediatR calls `_mapper.Map<>()` manually.

| | Fluxorq | MediatR 14 | Δ |
|---|---:|---:|---:|
| **Mean latency** | **132 ns** | 126 ns | ±5% (within noise) |
| **Allocated** | **352 B** | 384 B | **−8% less** |
| Gen0 collections | 0.0280 | 0.0305 | — |

```
Latency (ns)       0          75         150
               ─────────────────────────────
Fluxorq        █████████████████████░  132 ns
MediatR        ████████████████████░░  126 ns
```

```
Heap allocated (bytes)    0     100     200     300     400
                     ────────────────────────────────────────
Fluxorq              █████████████████████░░░░░  352 B  ✓ less
MediatR              ████████████████████████░░  384 B
```

_The mapping scenario is effectively tied — the 6 ns gap is within the margin of error (±2.3–2.7 ns). Fluxorq allocates 32 fewer bytes because `MappedRequestHandler` avoids the extra boxing that manual `.Map<>()` calls incur._

### Optimization history (Fluxorq only)

| Scenario | v1.0 (baseline) | After Phase 1 | After Phase 2 | Total improvement |
|---|---:|---:|---:|---:|
| Pure dispatch — latency | 143 ns | 92 ns | **74 ns** | ▼ 48% |
| Pure dispatch — allocated | 416 B | 312 B | **168 B** | ▼ 60% |
| Dispatch + mapping — latency | 213 ns | 145 ns | **132 ns** | ▼ 38% |
| Dispatch + mapping — allocated | 600 B | 496 B | **352 B** | ▼ 41% |

_Phase 2 changes: replaced `ISendInvoker → Task<object?>` (boxing) with `RequestInvoker<TResponse>` abstract base (generic on `TResponse`); removed `async` from both `Dispatcher.SendAsync` and the invoker — zero state-machine overhead, zero boxing on the dispatch hot path._

### Interpreting these numbers

In a production application doing any real work:

| Operation | Typical time |
|---|---:|
| PostgreSQL round-trip | 1–5 ms |
| HTTP call to internal service | 2–10 ms |
| Redis cache get | 200–500 µs |
| **Fluxorq dispatch overhead** | **< 0.075 µs** |

The dispatcher adds less than **0.0075%** to a 1 ms database query. It will not appear in any profiler trace of a real system.

---

## Migrating from MediatR

Most migrations complete in under an hour. The API shapes are intentionally similar.

### Step 1 — Swap NuGet packages

```bash
dotnet remove package MediatR
dotnet remove package MediatR.Extensions.Microsoft.DependencyInjection

dotnet add package Fluxorq
dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

### Step 2 — Update namespace imports

| Before | After |
|---|---|
| `using MediatR;` | `using Fluxorq.Abstractions;` |
| `using MediatR.Pipeline;` | `using Fluxorq.Pipeline;` |

### Step 3 — Rename the handler method

```diff
- public async Task<UserDto> Handle(GetUserQuery req, CancellationToken ct)
+ public async Task<UserDto> HandleAsync(GetUserQuery req, CancellationToken ct = default)
```

Global find-and-replace: `Task Handle(` → `Task HandleAsync(`

### Step 4 — Replace the call site

```diff
- private readonly IMediator _mediator;
+ private readonly IDispatcher _dispatcher;

- return Ok(await _mediator.Send(new GetUserQuery(id), ct));
+ return Ok(await _dispatcher.SendAsync(new GetUserQuery(id), ct));
```

### Step 5 — Replace DI registration

```diff
- services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
+ services.AddFluxorq(opts => opts.ScanAssemblyContaining<Program>());
```

### Step 6 — Migrate pipeline behaviors

| MediatR | Fluxorq |
|---|---|
| `IPipelineBehavior<TReq, TRes>` | `IPipelineBehavior<TReq, TRes>` *(same shape)* |
| `RequestHandlerDelegate<TRes> next` | `NextDelegate<TRes> next` |
| `Handle(req, next, ct)` | `HandleAsync(req, next, ct)` |

```diff
  public class LoggingBehavior<TRequest, TResponse>
      : IPipelineBehavior<TRequest, TResponse>
+     where TRequest : IRequest<TResponse>
  {
-     public async Task<TResponse> Handle(
-         TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
+     public async Task<TResponse> HandleAsync(
+         TRequest request, NextDelegate<TResponse> next, CancellationToken ct = default)
      {
          // same body — next() call is identical
      }
  }
```

### Step 7 — Replace `INotification` / publish-subscribe

MediatR's publish/subscribe has no built-in equivalent in Fluxorq (Fluxorq is request/response only). Options:

- **Keep domain events in-process**: use `Channel<T>` or a simple `IEventBus` abstraction
- **Use a proper message broker**: MassTransit, Wolverine, or NServiceBus
- **Fire-and-forget**: dispatch a `IRequest<Unit>` handler that raises side effects

### Migration checklist

- [ ] NuGet packages replaced  
- [ ] `using` statements updated  
- [ ] `Handle` → `HandleAsync` (method rename)  
- [ ] `IMediator`/`ISender` → `IDispatcher`  
- [ ] `.Send(...)` → `.SendAsync(...)`  
- [ ] DI registration updated  
- [ ] Pipeline behavior `RequestHandlerDelegate` → `NextDelegate`  
- [ ] `INotification` handling removed or replaced  
- [ ] Tests passing  

> Full migration guide with more detail: [docs/MigrationFromMediatR.md](docs/MigrationFromMediatR.md)

---

MIT — see [LICENSE](LICENSE).

> **Disclaimer**: Fluxorq is an original implementation. It is not affiliated with, derived from, or a modification of MediatR, AutoMapper, or any other library. Common architectural patterns referenced here (mediator, middleware) are well-understood industry conventions.
