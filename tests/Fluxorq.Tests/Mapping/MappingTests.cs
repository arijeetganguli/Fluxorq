using Fluxorq.Mapping;
using Fluxorq.Tests.Helpers;
using Mapture;
using Mapture.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Fluxorq.Tests.Mapping;

public class MappingExtensionsTests
{
    private IMapper BuildMapper()
    {
        var services = new ServiceCollection();
        services.AddMapture(typeof(UserProfile).Assembly);
        return services.BuildServiceProvider().GetRequiredService<IMapper>();
    }

    [Fact]
    public void MapTo_MapsConventionProperties()
    {
        var mapper = BuildMapper();
        var entity = new UserEntity { Id = 42, FirstName = "Jane", LastName = "Doe" };

        var dto = entity.MapTo<UserDto>(mapper);

        Assert.Equal(42, dto.Id);
        Assert.Equal("Jane Doe", dto.FullName);
    }

    [Fact]
    public void MapTo_CalledTwice_UsesCachedDelegate()
    {
        var mapper = BuildMapper();
        var e1 = new UserEntity { Id = 1, FirstName = "A", LastName = "B" };
        var e2 = new UserEntity { Id = 2, FirstName = "C", LastName = "D" };

        var d1 = e1.MapTo<UserDto>(mapper);
        var d2 = e2.MapTo<UserDto>(mapper);

        Assert.Equal(1, d1.Id);
        Assert.Equal(2, d2.Id);
    }

    [Fact]
    public void MapTo_NullSource_ThrowsArgumentNullException()
    {
        var mapper = BuildMapper();
        Assert.Throws<ArgumentNullException>(() => ((object)null!).MapTo<UserDto>(mapper));
    }

    [Fact]
    public void MapTo_NullMapper_ThrowsArgumentNullException()
    {
        var entity = new UserEntity { Id = 1, FirstName = "X", LastName = "Y" };
        Assert.Throws<ArgumentNullException>(() => entity.MapTo<UserDto>(null!));
    }
}

public class MappedRequestHandlerTests
{
    private static IServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddMapture(typeof(UserProfile).Assembly);
        services.AddTransient<GetUserHandler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task MappedHandler_ResolvesAndMapsEntity()
    {
        var sp = BuildProvider();
        var handler = sp.GetRequiredService<GetUserHandler>();

        var dto = await handler.HandleAsync(new GetUserRequest(1));

        Assert.Equal(1, dto.Id);
        Assert.Equal("Alice Smith", dto.FullName);
    }

    [Fact]
    public async Task MappedHandler_UnknownId_PropagatesException()
    {
        var sp = BuildProvider();
        var handler = sp.GetRequiredService<GetUserHandler>();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => handler.HandleAsync(new GetUserRequest(999)));
    }
}
