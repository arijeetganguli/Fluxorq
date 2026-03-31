# Replacing AutoMapper with Mapture in a Fluxorq Application

Fluxorq uses [Mapture](https://github.com/arijeetganguli/Mapture) as its mapping layer. Mapture maintains API compatibility with AutoMapper so most migrations take under 30 minutes.

---

## Step 1 — Replace NuGet packages

```bash
dotnet remove package AutoMapper
dotnet remove package AutoMapper.Extensions.Microsoft.DependencyInjection

dotnet add package Mapture
dotnet add package Mapture.Extensions.DependencyInjection
```

---

## Step 2 — Update namespaces

| AutoMapper | Mapture |
|---|---|
| `using AutoMapper;` | `using Mapture;` |
| `using AutoMapper.Extensions.Microsoft.DependencyInjection;` | `using Mapture.Extensions.DependencyInjection;` |

---

## Step 3 — Replace DI registration

**Before:**
```csharp
services.AddAutoMapper(typeof(Program).Assembly);
```

**After:**
```csharp
services.AddMapture(typeof(Program).Assembly);
```

Optional Mapture configuration:
```csharp
services.AddMapture(typeof(Program).Assembly, options =>
{
    options.MaxDepth = 10;              // prevent deep-recursion (default 10)
    options.EnableCycleDetection = true; // safe circular-reference handling (default true)
});
```

---

## Step 4 — Profiles are identical

AutoMapper `Profile` and Mapture `Profile` share the same API surface. No changes needed:

```csharp
// Works unchanged with Mapture
public class UserProfile : Profile
{
    public UserProfile()
    {
        CreateMap<User, UserDto>()
            .ForMember(d => d.FullName,
                opt => opt.MapFrom((Func<User, string>)(s => $"{s.First} {s.Last}")))
            .Ignore(d => d.InternalId)
            .ReverseMap();
    }
}
```

---

## Step 5 — IMapper usage is identical

```csharp
// Inject IMapper from Mapture — same as AutoMapper
public class UsersController : ControllerBase
{
    private readonly IMapper _mapper;
    public UsersController(IMapper mapper) => _mapper = mapper;

    [HttpGet]
    public IActionResult Get()
    {
        var users = _userService.GetAll();
        return Ok(users.Select(u => _mapper.Map<User, UserDto>(u)));
    }
}
```

---

## Step 6 — Use the Fluxorq `MapTo<>` shorthand (optional)

After installing Fluxorq you gain a compiled-delegate extension method:

```csharp
using Fluxorq.Mapping;

// Instead of:
var dto = _mapper.Map<User, UserDto>(user);

// You can write:
var dto = user.MapTo<UserDto>(_mapper);
```

---

## Step 7 — Use MappedRequestHandler for Resolve → Map handlers

Instead of repeating the fetch + map pattern in every handler, derive from `MappedRequestHandler`:

**Before (with AutoMapper):**
```csharp
public class GetUserHandler : IRequestHandler<GetUserQuery, UserDto>
{
    private readonly IUserRepository _repo;
    private readonly IMapper _mapper;

    public GetUserHandler(IUserRepository repo, IMapper mapper)
    {
        _repo   = repo;
        _mapper = mapper;
    }

    public async Task<UserDto> HandleAsync(GetUserQuery req, CancellationToken ct = default)
    {
        var user = await _repo.GetByIdAsync(req.Id, ct);
        return _mapper.Map<User, UserDto>(user);
    }
}
```

**After (with Fluxorq + Mapture):**
```csharp
public class GetUserHandler : MappedRequestHandler<GetUserQuery, User, UserDto>
{
    private readonly IUserRepository _repo;

    public GetUserHandler(IUserRepository repo, IMapper mapper) : base(mapper)
        => _repo = repo;

    protected override Task<User> ResolveAsync(GetUserQuery req, CancellationToken ct)
        => _repo.GetByIdAsync(req.Id, ct);
}
```

The base class calls `Mapper.Map<User, UserDto>(entity)` and returns the result — you just focus on the data retrieval.

---

## Key differences

| Feature | AutoMapper | Mapture |
|---|---|---|
| Performance | 4th (benchmarked) | 2nd — compiled delegates cached per type pair |
| Cycle detection | None — StackOverflowException | Built-in, configurable |
| Telemetry | Optional but present in some builds | Zero telemetry |
| .NET Framework 4.8 | ✅ | ✅ |
| .NET Standard 2.0 | ❌ (as of v12+) | ✅ |

---

## Checklist

- [ ] NuGet packages swapped  
- [ ] Namespace `using` statements updated  
- [ ] `services.AddAutoMapper(...)` → `services.AddMapture(...)`  
- [ ] Tests updated and passing (AutoMapper compatibility is high — most pass without changes)  
- [ ] Optionally adopt `MapTo<>` and `MappedRequestHandler<>` to reduce boilerplate  
