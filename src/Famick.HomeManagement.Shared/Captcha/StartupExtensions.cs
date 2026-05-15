using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Shared.Captcha;

public static class StartupExtensions
{
    /// <summary>
    /// Binds <see cref="RecaptchaSettings"/> from the <c>RecaptchaSettings</c>
    /// configuration section and registers <see cref="ICaptchaService"/>.
    /// If <see cref="RecaptchaSettings.SecretKey"/> is empty after binding,
    /// registers <see cref="NoOpCaptchaService"/> as the implementation —
    /// self-hosted deployments and dev environments get a silent pass without
    /// any extra wiring. Otherwise registers <see cref="RecaptchaService"/>
    /// as a typed <see cref="HttpClient"/>.
    /// </summary>
    public static IServiceCollection AddCaptcha(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("RecaptchaSettings");
        services.Configure<RecaptchaSettings>(section);

        var settings = section.Get<RecaptchaSettings>() ?? new RecaptchaSettings();
        if (string.IsNullOrWhiteSpace(settings.SecretKey))
        {
            services.AddSingleton<ICaptchaService, NoOpCaptchaService>();
        }
        else
        {
            services.AddHttpClient<ICaptchaService, RecaptchaService>();
        }

        return services;
    }
}
