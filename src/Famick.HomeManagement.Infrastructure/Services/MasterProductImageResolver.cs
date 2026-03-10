using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MasterProductImageResolver : IMasterProductImageResolver
{
    private readonly IFileStorageService _fileStorageService;
    private readonly string _baseUrl;

    public MasterProductImageResolver(IFileStorageService fileStorageService, string baseUrl)
    {
        _fileStorageService = fileStorageService;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    public string? GetImageUrl(string? imageSlug, bool hasLicensedImage, Guid masterProductId, Guid? primaryImageId)
    {
        // Priority 1: Licensed primary image (cloud-only, served via authenticated API)
        if (hasLicensedImage && primaryImageId.HasValue)
        {
            return _fileStorageService.GetMasterProductImageUrl(masterProductId, primaryImageId.Value);
        }

        // Priority 2: Free static image from the UI RCL (PNG for mobile compatibility)
        if (!string.IsNullOrEmpty(imageSlug))
        {
            return $"{_baseUrl}/_content/Famick.HomeManagement.UI/images/master-products/{imageSlug}.png";
        }

        // Priority 3: No image available — caller should fall back to IconSvg or generic icon
        return null;
    }
}
