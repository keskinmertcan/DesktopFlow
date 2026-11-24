using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace DesktopFlow.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IDbContextFactory<DesktopFlowDbContext> _factory;
    private readonly ISettingsStore _settings;
    private readonly IAppLogger _logger;

    public DatabaseInitializer(
        IDbContextFactory<DesktopFlowDbContext> factory,
        ISettingsStore settings,
        IAppLogger logger)
    {
        _factory = factory;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var preferences = await _settings.GetAsync(cancellationToken);
        var changed = false;
        if (string.IsNullOrWhiteSpace(preferences.DesktopPath))
        {
            preferences.DesktopPath = SystemFolders.GetDesktop();
            changed = true;
        }

        if (string.IsNullOrWhiteSpace(preferences.DownloadsPath))
        {
            preferences.DownloadsPath = SystemFolders.GetDownloads();
            changed = true;
        }

        if (preferences.DesktopMappings.Count == 0)
        {
            preferences.DesktopMappings = DefaultTaxonomy.CreateDesktopMappings();
            changed = true;
        }

        if (preferences.DownloadsMappings.Count == 0)
        {
            preferences.DownloadsMappings = DefaultTaxonomy.CreateDownloadsMappings();
            changed = true;
        }

        if (preferences.ExcludedPatterns.Count == 0)
        {
            preferences.ExcludedPatterns = ["desktop.ini"];
            changed = true;
        }

        if (changed)
        {
            await _settings.SaveAsync(preferences, cancellationToken);
        }

        _logger.Info("Database is ready.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
