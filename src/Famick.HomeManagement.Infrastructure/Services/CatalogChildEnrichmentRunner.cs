using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Plugin.Abstractions.ProductLookup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Famick.HomeManagement.Infrastructure.Services;

/// <summary>Fills in new catalog child products from the lookup plugins in the background.</summary>
public interface ICatalogChildEnrichmentRunner
{
    /// <summary>
    /// Starts <see cref="ICatalogChildProductService.EnrichAsync"/> for the product, as the household
    /// that created it, and returns at once.
    /// </summary>
    void Start(Guid tenantId, Guid productId, string barcode, string provisionalName, ProductLookupLocation? location);
}

/// <summary>
/// Runs each lookup on its own scope, after the scan or move to inventory that created the product has
/// returned, so a slow or failing plugin never holds either up. Nothing is queued durably: a lookup
/// cut short by a restart leaves the product with its provisional name, to be edited by hand.
/// </summary>
public sealed class CatalogChildEnrichmentRunner(
    IServiceScopeFactory scopeFactory,
    IHostApplicationLifetime lifetime,
    ILogger<CatalogChildEnrichmentRunner> logger) : ICatalogChildEnrichmentRunner
{
    /// <summary>The lookup's own limit and the saves after it.</summary>
    public static readonly TimeSpan RunLimit = CatalogChildProductService.LookupLimit + TimeSpan.FromSeconds(30);

    public void Start(Guid tenantId, Guid productId, string barcode, string provisionalName, ProductLookupLocation? location)
    {
        // Not on the request's execution context: its HttpContext, and the household read from it,
        // end with the response.
        using (ExecutionContext.SuppressFlow())
            _ = Task.Run(() => RunAsync(tenantId, productId, barcode, provisionalName, location));
    }

    private async Task RunAsync(Guid tenantId, Guid productId, string barcode, string provisionalName, ProductLookupLocation? location)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(lifetime.ApplicationStopping);
        stop.CancelAfter(RunLimit);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantProvider>().SetTenantId(tenantId);
            await scope.ServiceProvider.GetRequiredService<ICatalogChildProductService>()
                .EnrichAsync(productId, barcode, provisionalName, location, stop.Token);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Filling in product {ProductId} from barcode {Barcode} stopped unexpectedly", productId, barcode);
        }
    }
}
