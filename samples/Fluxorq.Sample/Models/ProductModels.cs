using Fluxorq.Abstractions;

namespace Fluxorq.Sample.Models;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

public class ProductDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool InStock { get; set; }
}

// ─── Requests ─────────────────────────────────────────────────────────────────

public record CreateProductRequest(string Name, decimal Price, int InitialStock)
    : IRequest<ProductDto>;

public record GetProductRequest(Guid Id)
    : IRequest<ProductDto>;

public record ListProductsRequest
    : IRequest<IReadOnlyList<ProductDto>>;
