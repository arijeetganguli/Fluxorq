using Fluxorq.Abstractions;
using Fluxorq.Mapping;
using Fluxorq.Tests.Helpers;
using Mapture;

namespace Fluxorq.Tests.Helpers;

// ─── Simple handlers ──────────────────────────────────────────────────────────

public class PingHandler : IRequestHandler<PingRequest, PingResponse>
{
    public Task<PingResponse> HandleAsync(PingRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PingResponse(request.Message));
}

public class AddHandler : IRequestHandler<AddRequest, int>
{
    public Task<int> HandleAsync(AddRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(request.A + request.B);
}

public class VoidHandler : IRequestHandler<VoidRequest, Unit>
{
    public Task<Unit> HandleAsync(VoidRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new Unit());
}

// ─── A handler that throws ────────────────────────────────────────────────────

public class FaultingHandler : IRequestHandler<PingRequest, PingResponse>
{
    public Task<PingResponse> HandleAsync(PingRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Simulated handler failure");
}

// ─── Handler that respects cancellation ──────────────────────────────────────

public class CancellingHandler : IRequestHandler<PingRequest, PingResponse>
{
    public async Task<PingResponse> HandleAsync(PingRequest request, CancellationToken cancellationToken = default)
    {
        await Task.Delay(5000, cancellationToken);
        return new PingResponse(request.Message);
    }
}

// ─── MappedRequestHandler implementation ─────────────────────────────────────

public class GetUserHandler : MappedRequestHandler<GetUserRequest, UserEntity, UserDto>
{
    private static readonly Dictionary<int, UserEntity> _store = new()
    {
        [1] = new UserEntity { Id = 1, FirstName = "Alice", LastName = "Smith" },
        [2] = new UserEntity { Id = 2, FirstName = "Bob",   LastName = "Jones" },
    };

    public GetUserHandler(IMapper mapper) : base(mapper) { }

    protected override Task<UserEntity> ResolveAsync(GetUserRequest request, CancellationToken cancellationToken)
    {
        _store.TryGetValue(request.Id, out var user);
        return Task.FromResult(user ?? throw new KeyNotFoundException($"User {request.Id} not found."));
    }
}
