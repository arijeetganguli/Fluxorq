using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Mapture;

namespace Fluxorq.Mapping;

/// <summary>
/// Extension methods that add fluent mapping to any object using a Mapture <see cref="IMapper"/>.
///
/// On first use for a source/destination type pair, a compiled expression delegate is built and
/// cached — subsequent calls pay only a dictionary lookup and a delegate invocation.
/// </summary>
public static class MappingExtensions
{
    private static readonly ConcurrentDictionary<(Type Source, Type Destination), Func<IMapper, object, object>> _delegates = new();

    private static readonly MethodInfo _mapMethod =
        typeof(IMapper).GetMethods()
            .First(m => m.Name == "Map" && m.IsGenericMethod && m.GetGenericArguments().Length == 2);

    /// <summary>
    /// Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>
    /// using the provided <paramref name="mapper"/>.
    /// </summary>
    /// <typeparam name="TDestination">The type to map to.</typeparam>
    /// <param name="source">The object to map from. Must not be null.</param>
    /// <param name="mapper">The Mapture mapper to use. Must not be null.</param>
    /// <returns>A new instance of <typeparamref name="TDestination"/> populated from <paramref name="source"/>.</returns>
    public static TDestination MapTo<TDestination>(this object source, IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapper);

        var key = (source.GetType(), typeof(TDestination));
        var mapDelegate = _delegates.GetOrAdd(key, static k => CompileMapDelegate(k.Source, k.Destination));
        return (TDestination)mapDelegate(mapper, source);
    }

    private static Func<IMapper, object, object> CompileMapDelegate(Type sourceType, Type destinationType)
    {
        var typedMethod = _mapMethod.MakeGenericMethod(sourceType, destinationType);

        var mapperParam = Expression.Parameter(typeof(IMapper), "mapper");
        var sourceParam = Expression.Parameter(typeof(object), "source");

        var castSource = Expression.Convert(sourceParam, sourceType);
        var callMap = Expression.Call(mapperParam, typedMethod, castSource);
        var castResult = Expression.Convert(callMap, typeof(object));

        return Expression.Lambda<Func<IMapper, object, object>>(castResult, mapperParam, sourceParam).Compile();
    }
}
