using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Organization;
using DesktopFlow.Core.Rules;
using DesktopFlow.Infrastructure.Files;
using DesktopFlow.Infrastructure.Persistence;
using DesktopFlow.Infrastructure.Storage;
using DesktopFlow.Infrastructure.SystemServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopFlow.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopFlowInfrastructure(this IServiceCollection services)
    {
        return services.AddDesktopFlowInfrastructure(AppDataPaths.ForUser());
    }

    public static IServiceCollection AddDesktopFlowInfrastructure(this IServiceCollection services, AppDataPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        paths.EnsureCreated();
        services.AddSingleton(paths);
        services.AddSingleton<IAppLogger>(_ => new FileAppLogger(paths.LogFile));
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<SqliteGate>();
        services.AddDbContextFactory<DesktopFlowDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabaseFile};Cache=Shared"));
        services.AddSingleton<ISettingsStore, SqliteSettingsStore>();
        services.AddSingleton<IActivityStore, SqliteActivityStore>();
        services.AddSingleton<IRuleStore, SqliteRuleStore>();
        services.AddSingleton<IFileScanner, FileScanner>();
        services.AddSingleton<IStorageAnalyzer, StorageAnalyzer>();
        services.AddSingleton<IFileLockProbe, FileLockProbe>();
        services.AddSingleton<IFileStabilityWaiter, FileStabilityWaiter>();
        services.AddSingleton<IFileMover, SafeFileMover>();
        services.AddSingleton<IFileClassifier, FileClassifier>();
        services.AddSingleton<IRuleEngine, RuleEngine>();
        services.AddSingleton<OrganizationPlanner>();
        services.AddSingleton<IOrganizationService, OrganizationService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IRecoveryService, RecoveryService>();
        services.AddSingleton<IShellLauncher, WindowsShellLauncher>();
        services.AddSingleton<IUpdateChecker, GitHubUpdateChecker>();
        services.AddSingleton<INotificationService, NullNotificationService>();
        services.AddHostedService<DatabaseInitializer>();
        services.AddSingleton<FileMonitorService>();
        services.AddSingleton<IMonitorController>(provider => provider.GetRequiredService<FileMonitorService>());
        services.AddHostedService(provider => provider.GetRequiredService<FileMonitorService>());
        return services;
    }
}
