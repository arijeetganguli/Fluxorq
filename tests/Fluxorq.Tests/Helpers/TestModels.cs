using Fluxorq.Abstractions;
using Mapture;

namespace Fluxorq.Tests.Helpers;

// ─── Simple request / response types ─────────────────────────────────────────

public record PingRequest(string Message) : IRequest<PingResponse>;
public record PingResponse(string Echo);

public record AddRequest(int A, int B) : IRequest<int>;

public record VoidRequest : IRequest<Unit>;

/// <summary>Represents a valueless response.</summary>
public record Unit;

// ─── Domain models for mapping tests ─────────────────────────────────────────

public class UserEntity
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class UserDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
}

// ─── Mapture Profile ──────────────────────────────────────────────────────────

public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<UserEntity, UserDto>()
            .ForMember(d => d.FullName,
                opt => opt.MapFrom((Func<UserEntity, string>)(s => $"{s.FirstName} {s.LastName}")));
    }
}

// ─── Request for MappedHandler tests ─────────────────────────────────────────

public record GetUserRequest(int Id) : IRequest<UserDto>;
