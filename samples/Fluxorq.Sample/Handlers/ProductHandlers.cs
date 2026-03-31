using Fluxorq.Abstractions;
using Fluxorq.Mapping;
using Fluxorq.Sample.Models;
using Mapture;

namespace Fluxorq.Sample.Handlers;

/// <summary>
/// Creates a product and maps the saved entity back to a DTO.
/// Demonstrates MappedRequestHandler for write operations.
/// </summary>
public class CreateProductHandler : MappedRequestHandler<CreateProductRequest, Product, ProductDto>
{
    private readonly IProductRepository _repo;

    public CreateProductHandler(IProductRepository repo, IMapper mapper) : base(mapper)
        => _repo = repo;

    protected override async Task<Product> ResolveAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Price = request.Price,
            StockQuantity = request.InitialStock
        };

        await _repo.AddAsync(product, cancellationToken);
        return product;
    }
}

/// <summary>
/// Fetches a single product by ID. Demonstrates MappedRequestHandler for read operations.
/// </summary>
public class GetProductHandler : MappedRequestHandler<GetProductRequest, Product, ProductDto>
{
    private readonly IProductRepository _repo;

    public GetProductHandler(IProductRepository repo, IMapper mapper) : base(mapper)
        => _repo = repo;

    protected override async Task<Product> ResolveAsync(GetProductRequest request, CancellationToken cancellationToken)
    {
        var product = await _repo.GetByIdAsync(request.Id, cancellationToken);
        return product ?? throw new KeyNotFoundException($"Product '{request.Id}' was not found.");
    }
}

/// <summary>
/// Lists all products. Demonstrates a handler that returns a collection.
/// </summary>
public class ListProductsHandler : IRequestHandler<ListProductsRequest, IReadOnlyList<ProductDto>>
{
    private readonly IProductRepository _repo;
    private readonly IMapper _mapper;

    public ListProductsHandler(IProductRepository repo, IMapper mapper)
    {
        _repo = repo;
        _mapper = mapper;
    }

    public async Task<IReadOnlyList<ProductDto>> HandleAsync(
        ListProductsRequest request,
        CancellationToken cancellationToken = default)
    {
        var products = await _repo.GetAllAsync(cancellationToken);
        return products.Select(p => _mapper.Map<Product, ProductDto>(p)).ToList();
    }
}
