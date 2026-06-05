namespace Famick.HomeManagement.Core.DTOs.Server;

/// <summary>
/// Shape of the self-hosted <c>config/server-config.json</c> overlay. The wizard
/// and the admin "Server Settings" page round-trip this DTO through
/// <see cref="Interfaces.IServerConfigService"/>. Nested sections are nullable —
/// a null section means "inherit from the baked-in <c>appsettings.json</c>".
/// </summary>
public class ServerConfigDto
{
    public ServerSection Server { get; set; } = new();
    public ServerEmailSection? EmailSettings { get; set; }
    public ServerJwtSection? JwtSettings { get; set; }
    public ServerPluginsSection? Plugins { get; set; }
}

public class ServerSection
{
    public bool SetupComplete { get; set; }
    public string PublicHostName { get; set; } = "https://localhost";
    public string TimeZone { get; set; } = "UTC";
}

public class ServerEmailSection
{
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public bool? EnableSsl { get; set; }
    public string? FromEmail { get; set; }
    public string? FromName { get; set; }
}

public class ServerJwtSection
{
    public string? Issuer { get; set; }
    public string? Audience { get; set; }
}

public class ServerPluginsSection
{
    public string? Path { get; set; }
}
