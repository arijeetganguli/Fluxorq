using Fluxorq.Abstractions;
using Mapture;

namespace Fluxorq.Mapping;

/// <summary>
/// Base class for handlers that follow the Resolve → Map pattern:
/// fetch an entity of type <typeparamref name="TEntity"/> from a data source,
/// then use Mapture to project it to <typeparamref name="TResponse"/>.
///
/// <example>
/// <code>
/// public class GetProductHandler : MappedRequestHandler&lt;GetProductRequest, Product, ProductDto&gt;
/// {
///     private readonly IProductRepository _repo;
///
///     public GetProductHandler(IProductRepository repo, IMapper mapper) : base(mapper)
///         => _repo = repo;
///
///     protected override async Task&lt;Product&gt; ResolveAsync(GetProductRequest request, CancellationToken ct)
///         => await _repo.GetByIdAsync(request.Id, ct)
///            ?? throw new KeyNotFoundException($"Product {request.Id} not found.");
/// }
/// </code>
/// </example>
/// </summary>
/// <typeparam name="TRequest">The request type. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TEntity">The domain entity or data model resolved by <see cref="ResolveAsync"/>.</typeparam>
/// <typeparam name="TResponse">The DTO or response type the entity is mapped to.</typeparam>
public abstract class MappedRequestHandler<TRequest, TEntity, TResponse>
    : IRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>The Mapture mapper used to project <typeparamref name="TEntity"/> to <typeparamref name="TResponse"/>.</summary>
    protected IMapper Mapper { get; }

    protected MappedRequestHandler(IMapper mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);
        Mapper = mapper;
    }

    /// <summary>
    /// Resolves the entity for the given request. Implement your data-access logic here.
    /// </summary>
    protected abstract Task<TEntity> ResolveAsync(TRequest request, CancellationToken cancellationToken);

    /// <inheritdoc/>
    public async Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await ResolveAsync(request, cancellationToken).ConfigureAwait(false);
        return Mapper.Map<TEntity, TResponse>(entity);
    }
}
