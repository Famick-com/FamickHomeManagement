using Famick.HomeManagement.Domain.Enums;

namespace Famick.HomeManagement.Core.DTOs.MealPlanner;

public class AllergenCheckResultDto
{
    public Guid MealId { get; set; }
    public bool HasWarnings { get; set; }
    public List<AllergenWarningDto> Warnings { get; set; } = new();
}

public class AllergenWarningDto
{
    public Guid ContactId { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public AllergenType AllergenType { get; set; }
    public AllergenSeverity Severity { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public Guid? ProductId { get; set; }
}
