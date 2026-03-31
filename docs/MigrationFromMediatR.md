# Migration Guide: MediatR → Fluxorq

This guide helps you migrate an existing MediatR codebase to Fluxorq. Most migrations are mechanical find-and-replace operations. Typical teams complete the migration in under an hour for a medium-sized application.

---

## Step 1 — Replace NuGet packages

```bash
# Remove MediatR
dotnet remove package MediatR
dotnet remove package MediatR.Extensions.Microsoft.DependencyInjection

# Add Fluxorq
dotnet add package Fluxorq
dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

---

## Step 2 — Find and replace namespaces

| MediatR | Fluxorq |
|---|---|
| `using MediatR;` | `using Fluxorq.Abstractions;` |
| `using MediatR.Pipeline;` | `using Fluxorq.Pipeline;` |

---

## Step 3 — Replace request interfaces

| MediatR | Fluxorq |
|---|---|
| `IRequest<TResponse>` | `IRequest<TResponse>` *(same name, different namespace)* |
| `IRequest` (void) | `IRequest<Unit>` where `Unit` is your own empty record |
| `INotification` | Not in Fluxorq (use your own event bus or `IObservable<>`) |

**Before:**
```csharp
using MediatR;

public record GetUserQuery(int Id) : IRequest<UserDto>;
```

**After:**
```csharp
using Fluxorq.Abstractions;

public record GetUserQuery(int Id) : IRequest<UserDto>;
```

---

## Step 4 — Replace handler interfaces

| MediatR | Fluxorq |
|---|---|
| `IRequestHandler<TReq, TRes>` | `IRequestHandler<TReq, TRes>` *(same shape)* |
| `Handle(request, ct)` | `HandleAsync(request, ct)` |

**Before:**
```csharp
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
    {
        // ...
    }
}
```

**After:**
```csharp
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> HandleAsync(GetUserQuery request, CancellationToken ct = default)
    {
        // ...
    }
}
```

> **Tip**: use a global find-and-replace for `Task Handle(` → `Task HandleAsync(`.

---

## Step 5 — Replace the mediator call site

| MediatR | Fluxorq |
|---|---|
| `IMediator` | `IDispatcher` |
| `mediator.Send(request, ct)` | `dispatcher.SendAsync(request, ct)` |
| `mediator.Publish(notification, ct)` | Remove or replace with your event bus |

**Before:**
```csharp
public class UsersController : ControllerBase
{
    private readonly IMediator _mediator;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetUserQuery(id), ct));
}
```

**After:**
```csharp
public class UsersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
        => Ok(await _dispatcher.SendAsync(new GetUserQuery(id), ct));
}
```

---

## Step 6 — Replace DI registration

**Before:**
```csharp
services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));
```

**After:**
```csharp
services.AddFluxorq(opts => opts.ScanAssemblyContaining<Program>());
```

---

## Step 7 — Migrate pipeline behaviors

| MediatR | Fluxorq |
|---|---|
| `IPipelineBehavior<TReq, TRes>` | `IPipelineBehavior<TReq, TRes>` *(same shape)* |
| `RequestHandlerDelegate<TRes> next` | `NextDelegate<TRes> next` |
| `Handle(req, next, ct)` | `HandleAsync(req, next, ct)` |
| `next()` | `next()` *(identical)* |

**Before:**
```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var result = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return result;
    }
}
```

**After:**
```csharp
public class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        NextDelegate<TResponse> next,
        CancellationToken ct = default)
    {
        Console.WriteLine($"Handling {typeof(TRequest).Name}");
        var result = await next();
        Console.WriteLine($"Handled {typeof(TRequest).Name}");
        return result;
    }
}
```

---

## Step 8 — Remove INotification / INotificationHandler

MediatR's publish/subscribe model has no equivalent in Fluxorq (Fluxorq focuses purely on request/response). Options:

- Use a dedicated event bus (MassTransit, Wolverine, in-process `Channel<T>`)  
- Raise domain events after dispatch in your application service  
- Keep a lightweight `IEventPublisher` interface in your application layer  

---

## Checklist

- [ ] NuGet packages replaced  
- [ ] Namespace `using` statements updated
- [ ] `Handle` → `HandleAsync` (method rename)  
- [ ] `IMediator` → `IDispatcher`  
- [ ] `mediator.Send(...)` → `dispatcher.SendAsync(...)`  
- [ ] DI registration updated  
- [ ] Pipeline behavior `Handle` → `HandleAsync`, `RequestHandlerDelegate` → `NextDelegate`  
- [ ] `INotification` handling replaced or removed  
- [ ] Tests updated and passing  
