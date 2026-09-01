using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Famick.HomeManagement.Jobs;

public static class StartupExtensions
{
    public static IServiceCollection AddJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddKeyedScoped<IJob, NotificationsDailyJob>("notifications-daily");
        services.AddKeyedScoped<IJob, CalendarRemindersJob>("calendar-reminders");
        services.AddKeyedScoped<IJob, ExternalCalendarSyncJob>("external-calendar-sync");
        services.AddKeyedScoped<IJob, AccountPurgeJob>("account-purge");
        return services;
    }
}
