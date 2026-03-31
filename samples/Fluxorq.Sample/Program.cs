using Fluxorq.Abstractions;
using Fluxorq.Abstractions.Exceptions;
using Fluxorq.DependencyInjection;
using Fluxorq.Sample;
using Fluxorq.Sample.Models;
using Mapture.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ─── DI Setup ─────────────────────────────────────────────────────────────────

var services = new ServiceCollection();

services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Debug));
services.AddMapture(typeof(Program).Assembly);
services.AddSingleton<IProductRepository, InMemoryProductRepository>();

services.AddFluxorq(opts => opts.ScanAssemblyContaining<Program>());

var sp = services.BuildServiceProvider();
var dispatcher = sp.GetRequiredService<IDispatcher>();

// ─── Create a product (goes through pipeline: Logging → Validation → Performance → Handler) ──

Console.WriteLine("=== Creating a product ===");
var created = await dispatcher.SendAsync(
    new CreateProductRequest("Fluxorq T-Shirt", 24.99m, 100));

Console.WriteLine($"Created: {created.Id} — {created.Name} — ${created.Price} — InStock: {created.InStock}");

// ─── Get by ID ────────────────────────────────────────────────────────────────

Console.WriteLine("\n=== Fetching product by ID ===");
var fetched = await dispatcher.SendAsync(new GetProductRequest(created.Id));
Console.WriteLine($"Fetched: {fetched.Name}");

// ─── List all ─────────────────────────────────────────────────────────────────

Console.WriteLine("\n=== Listing all products ===");
var list = await dispatcher.SendAsync(new ListProductsRequest());
foreach (var p in list)
    Console.WriteLine($"  - {p.Name} (${p.Price})");

// ─── Validation failure ───────────────────────────────────────────────────────

Console.WriteLine("\n=== Attempting invalid create (should fail validation) ===");
try
{
    await dispatcher.SendAsync(new CreateProductRequest("", -1m, 0));
}
catch (FluxorqValidationException ex)
{
    Console.WriteLine($"Validation failed ({ex.Failures.Count} error(s)):");
    foreach (var f in ex.Failures)
        Console.WriteLine($"  • {f}");
}

// ─── Not found ────────────────────────────────────────────────────────────────

Console.WriteLine("\n=== Fetching unknown product ID (should throw) ===");
try
{
    await dispatcher.SendAsync(new GetProductRequest(Guid.NewGuid()));
}
catch (KeyNotFoundException ex)
{
    Console.WriteLine($"Caught: {ex.Message}");
}

Console.WriteLine("\nDone.");
