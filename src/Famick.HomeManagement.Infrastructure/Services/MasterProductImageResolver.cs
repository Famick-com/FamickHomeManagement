using Famick.HomeManagement.Core.Interfaces;

namespace Famick.HomeManagement.Infrastructure.Services;

public class MasterProductImageResolver : IMasterProductImageResolver
{
    private readonly IFileStorageService _fileStorageService;

    public MasterProductImageResolver(IFileStorageService fileStorageService)
    {
        _fileStorageService = fileStorageService;
    }

    public string? GetImageUrl(string? imageSlug, bool hasLicensedImage, Guid masterProductId, Guid? primaryImageId)
    {
        // Priority 1: Licensed primary image (cloud-only, served via authenticated API)
        if (hasLicensedImage && primaryImageId.HasValue)
        {
            return _fileStorageService.GetMasterProductImageUrl(masterProductId, primaryImageId.Value);
        }

        // Priority 2: Free static SVG from the UI RCL
        if (!string.IsNullOrEmpty(imageSlug))
        {
            return $"/_content/Famick.HomeManagement.UI/images/master-products/{imageSlug}.svg";
        }

        // Priority 3: No image available — caller should fall back to IconSvg or generic icon
        return null;
    }
}
