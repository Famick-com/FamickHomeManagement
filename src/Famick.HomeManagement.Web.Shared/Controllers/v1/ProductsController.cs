using Famick.HomeManagement.Core.DTOs.Products;
using Famick.HomeManagement.Core.Interfaces;
using Famick.HomeManagement.Web.Shared.Controllers;
using Famick.HomeManagement.Web.Shared.Models;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Famick.HomeManagement.Web.Shared.Controllers.v1;

/// <summary>
/// API controller for managing products and their barcodes
/// </summary>
[ApiController]
[Route("api/v1/products")]
[Authorize]
public class ProductsController : ApiControllerBase
{
    private readonly IProductsService _productsService;
    private readonly IFileStorageService _fileStorage;
    private readonly IFileAccessTokenService _tokenService;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;

    public ProductsController(
        IProductsService productsService,
        IFileStorageService fileStorage,
        IFileAccessTokenService tokenService,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator,
        ITenantProvider tenantProvider,
        ILogger<ProductsController> logger)
        : base(tenantProvider, logger)
    {
        _productsService = productsService;
        _fileStorage = fileStorage;
        _tokenService = tokenService;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    #region Product CRUD

    /// <summary>
    /// Lists all products with optional filtering
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of products</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProductDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> List(
        [FromQuery] ProductFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Listing products for tenant {TenantId}", TenantId);

        var products = await _productsService.ListAsync(filter, cancellationToken);
        return ApiResponse(products);
    }

    /// <summary>
    /// Gets a specific product by ID
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting product {ProductId} for tenant {TenantId}", id, TenantId);

        var product = await _productsService.GetByIdAsync(id, cancellationToken);

        if (product == null)
        {
            return NotFoundResponse($"Product with ID {id} not found");
        }

        return ApiResponse(product);
    }

    /// <summary>
    /// Creates a new product
    /// </summary>
    /// <param name="request">Product creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created product</returns>
    [HttpPost]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Create(
        [FromBody] CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _createValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Creating product '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var product = await _productsService.CreateAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product
        );
    }

    /// <summary>
    /// Updates an existing product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="request">Product update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated product</returns>
    [HttpPut("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _updateValidator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationErrorResponse(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            );
        }

        _logger.LogInformation("Updating product {ProductId} for tenant {TenantId}", id, TenantId);

        var product = await _productsService.UpdateAsync(id, request, cancellationToken);
        return ApiResponse(product);
    }

    /// <summary>
    /// Gets dependency information for a product (stock, shopping lists, recipes)
    /// </summary>
    [HttpGet("{id}/dependencies")]
    [ProducesResponseType(typeof(ProductDependenciesDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetDependencies(
        Guid id,
        CancellationToken cancellationToken)
    {
        var dependencies = await _productsService.GetDependenciesAsync(id, cancellationToken);
        return ApiResponse(dependencies);
    }

    /// <summary>
    /// Deletes a product. Use force=true to also remove stock entries and shopping list items.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(422)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting product {ProductId} for tenant {TenantId} (force={Force})", id, TenantId, force);

        await _productsService.DeleteAsync(id, force, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Barcode Management

    /// <summary>
    /// Adds a barcode to a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="barcode">Barcode value</param>
    /// <param name="note">Optional note about the barcode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created barcode</returns>
    [HttpPost("{id}/barcodes")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductBarcodeDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddBarcode(
        Guid id,
        [FromQuery] string barcode,
        [FromQuery] string? note,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode))
        {
            return BadRequest(new { error_message = "Barcode is required" });
        }

        _logger.LogInformation("Adding barcode to product {ProductId} for tenant {TenantId}", id, TenantId);

        var productBarcode = await _productsService.AddBarcodeAsync(id, barcode, note, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id },
            productBarcode
        );
    }

    /// <summary>
    /// Deletes a barcode from a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="barcodeId">Barcode ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}/barcodes/{barcodeId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteBarcode(
        Guid id,
        Guid barcodeId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting barcode {BarcodeId} from product {ProductId} for tenant {TenantId}",
            barcodeId, id, TenantId);

        await _productsService.DeleteBarcodeAsync(barcodeId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Looks up a product by barcode
    /// </summary>
    /// <param name="barcode">Barcode value</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Product details if found</returns>
    [HttpGet("by-barcode/{barcode}")]
    [ProducesResponseType(typeof(ProductDto), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetByBarcode(
        string barcode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Looking up product by barcode '{Barcode}' for tenant {TenantId}", barcode, TenantId);

        var product = await _productsService.GetByBarcodeAsync(barcode, cancellationToken);

        if (product == null)
        {
            return NotFoundResponse($"Product with barcode '{barcode}' not found");
        }

        return ApiResponse(product);
    }

    #endregion

    #region Image Management

    private static readonly string[] AllowedImageTypes = { "image/jpeg", "image/png", "image/gif", "image/webp" };
    private const long MaxImageSize = 5 * 1024 * 1024; // 5MB

    /// <summary>
    /// Uploads one or more images to a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="uploadRequest">Upload request containing image files</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of uploaded image details</returns>
    [HttpPost("{id}/images")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(List<ProductImageDto>), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImages(
        Guid id,
        [FromForm] UploadProductImagesRequest uploadRequest,
        CancellationToken cancellationToken)
    {
        if (uploadRequest.Files == null || uploadRequest.Files.Count == 0)
        {
            return BadRequest(new { error_message = "At least one image file is required" });
        }

        // Validate files
        foreach (var file in uploadRequest.Files)
        {
            if (!AllowedImageTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                return BadRequest(new { error_message = $"File '{file.FileName}' is not a supported image type. Allowed: jpg, png, gif, webp" });
            }

            if (file.Length > MaxImageSize)
            {
                return BadRequest(new { error_message = $"File '{file.FileName}' exceeds the maximum size of 5MB" });
            }
        }

        _logger.LogInformation("Uploading {Count} images to product {ProductId} for tenant {TenantId}",
            uploadRequest.Files.Count, id, TenantId);

        var uploadedImages = new List<ProductImageDto>();

        foreach (var file in uploadRequest.Files)
        {
            await using var stream = file.OpenReadStream();
            var image = await _productsService.AddImageAsync(
                id,
                stream,
                file.FileName,
                file.ContentType,
                file.Length,
                cancellationToken);

            uploadedImages.Add(image);
        }

        return CreatedAtAction(nameof(GetImages), new { id }, uploadedImages);
    }

    /// <summary>
    /// Adds an image to a product from a URL
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="request">Request containing the image URL</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The uploaded image info</returns>
    [HttpPost("{id}/images/from-url")]
    [ProducesResponseType(typeof(ProductImageDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> AddImageFromUrl(
        Guid id,
        [FromBody] AddImageFromUrlRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.ImageUrl))
        {
            return BadRequest(new { error_message = "Image URL is required" });
        }

        // Validate URL format
        if (!Uri.TryCreate(request.ImageUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return BadRequest(new { error_message = "Invalid image URL. Must be a valid HTTP or HTTPS URL." });
        }

        _logger.LogInformation("Adding image from URL to product {ProductId} for tenant {TenantId}: {ImageUrl}",
            id, TenantId, request.ImageUrl);

        try
        {
            var image = await _productsService.AddImageFromUrlAsync(id, request.ImageUrl, cancellationToken);

            if (image == null)
            {
                return NotFoundResponse($"Product with ID {id} not found");
            }

            return CreatedAtAction(nameof(GetImages), new { id }, image);
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
        {
            _logger.LogWarning(ex, "Failed to fetch image from URL: {ImageUrl}", request.ImageUrl);
            return BadRequest(new { error_message = "Failed to fetch image from URL. The URL may be inaccessible or the request timed out." });
        }
    }

    /// <summary>
    /// Gets all images for a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of product images</returns>
    [HttpGet("{id}/images")]
    [ProducesResponseType(typeof(List<ProductImageDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetImages(
        Guid id,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting images for product {ProductId} for tenant {TenantId}", id, TenantId);

        var images = await _productsService.GetImagesAsync(id, cancellationToken);
        return ApiResponse(images);
    }

    /// <summary>
    /// Downloads a product image (secure file access with tenant validation).
    /// Accepts either Authorization header OR a valid access token in query string.
    /// </summary>
    /// <param name="productId">Product ID</param>
    /// <param name="imageId">Image ID</param>
    /// <param name="token">Optional access token for browser-initiated requests</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The image file</returns>
    [HttpGet("{productId}/images/{imageId}/download")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileResult), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> DownloadImage(
        Guid productId,
        Guid imageId,
        [FromQuery] string? token,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading image {ImageId} for product {ProductId}", imageId, productId);

        // Validate access and get expected tenant ID
        var expectedTenantId = ValidateFileAccess(_tokenService, token, "product-image", imageId);
        if (!expectedTenantId.HasValue)
        {
            return Unauthorized();
        }

        // Load image (uses IgnoreQueryFilters)
        var image = await _productsService.GetImageByIdAsync(productId, imageId, cancellationToken);
        if (image == null)
        {
            return NotFoundResponse("Image not found");
        }

        // Validate tenant access
        if (!ValidateTenantAccess(image.TenantId, expectedTenantId.Value))
        {
            return NotFoundResponse("Image not found");
        }

        var stream = await _fileStorage.GetProductImageStreamAsync(productId, image.FileName, cancellationToken);
        if (stream == null)
        {
            _logger.LogWarning("Image file not found: product {ProductId}, file {FileName}", productId, image.FileName);
            return NotFoundResponse("Image file not found");
        }

        // Return without filename to display inline (Content-Disposition: inline)
        // instead of triggering download (Content-Disposition: attachment)
        return File(stream, image.ContentType);
    }

    /// <summary>
    /// Deletes an image from a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="imageId">Image ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}/images/{imageId}")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> DeleteImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting image {ImageId} from product {ProductId} for tenant {TenantId}",
            imageId, id, TenantId);

        await _productsService.DeleteImageAsync(imageId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Sets an image as the primary image for a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="imageId">Image ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id}/images/{imageId}/primary")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> SetPrimaryImage(
        Guid id,
        Guid imageId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Setting image {ImageId} as primary for product {ProductId} for tenant {TenantId}",
            imageId, id, TenantId);

        await _productsService.SetPrimaryImageAsync(imageId, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Reorders images for a product
    /// </summary>
    /// <param name="id">Product ID</param>
    /// <param name="imageIds">Ordered list of image IDs</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpPut("{id}/images/reorder")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(204)]
    [ProducesResponseType(401)]
    [ProducesResponseType(404)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> ReorderImages(
        Guid id,
        [FromBody] List<Guid> imageIds,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reordering images for product {ProductId} for tenant {TenantId}", id, TenantId);

        await _productsService.ReorderImagesAsync(id, imageIds, cancellationToken);
        return NoContent();
    }

    #endregion

    #region Autocomplete & Quick Create

    /// <summary>
    /// Autocomplete search for products (lightweight, for inline add)
    /// </summary>
    [HttpGet("autocomplete")]
    [ProducesResponseType(typeof(List<ProductAutocompleteDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Autocomplete(
        [FromQuery] string q,
        [FromQuery] int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return ApiResponse(new List<ProductAutocompleteDto>());
        }

        var results = await _productsService.AutocompleteAsync(q, maxResults, cancellationToken);
        return ApiResponse(results);
    }

    /// <summary>
    /// Creates a product from external lookup data (barcode scan, store search, etc.)
    /// </summary>
    [HttpPost("from-lookup")]
    [Authorize(Policy = "RequireEditor")]
    [ProducesResponseType(typeof(ProductDto), 201)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> CreateFromLookup(
        [FromBody] CreateProductFromLookupRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationErrorResponse(new Dictionary<string, string[]>
            {
                { "Name", new[] { "Product name is required" } }
            });
        }

        _logger.LogInformation("Creating product from lookup '{Name}' for tenant {TenantId}", request.Name, TenantId);

        var product = await _productsService.CreateFromLookupAsync(request, cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = product.Id },
            product
        );
    }

    #endregion

    #region Stock & Search Features

    /// <summary>
    /// Gets current stock levels for all products
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of products with stock level information</returns>
    [HttpGet("stock-levels")]
    [ProducesResponseType(typeof(List<ProductStockLevelDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetStockLevels(
        [FromQuery] ProductFilterRequest? filter,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting stock levels for tenant {TenantId}", TenantId);

        var stockLevels = await _productsService.GetStockLevelsAsync(filter, cancellationToken);
        return ApiResponse(stockLevels);
    }

    /// <summary>
    /// Gets products with low stock (below minimum quantity)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of products with low stock</returns>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(List<ProductDto>), 200)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> GetLowStock(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting low stock products for tenant {TenantId}", TenantId);

        var products = await _productsService.GetLowStockProductsAsync(cancellationToken);
        return ApiResponse(products);
    }

    /// <summary>
    /// Searches for products by name, description, or barcode
    /// </summary>
    /// <param name="q">Search query</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of matching products</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<ProductDto>), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    [ProducesResponseType(500)]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { error_message = "Search query is required" });
        }

        _logger.LogInformation("Searching products with query '{Query}' for tenant {TenantId}", q, TenantId);

        var products = await _productsService.SearchAsync(q, cancellationToken);
        return ApiResponse(products);
    }

    #endregion
}
