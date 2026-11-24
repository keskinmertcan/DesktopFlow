using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Classification;
using DesktopFlow.Core.Models;
using DesktopFlow.Infrastructure.DependencyInjection;
using DesktopFlow.Infrastructure.Persistence;
using DesktopFlow.Infrastructure.Storage;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopFlow.Tests;

internal sealed class TestApp : IDisposable
{
    public TestApp()
    {
        Root = Path.Combine(Path.GetTempPath(), "DesktopFlowTests", Guid.NewGuid().ToString("N"));
        Desktop = Path.Combine(Root, "Desktop");
        Downloads = Path.Combine(Root, "Downloads");
        Directory.CreateDirectory(Desktop);
        Directory.CreateDirectory(Downloads);
        var services = new ServiceCollection();
        services.AddDesktopFlowInfrastructure(new AppDataPaths(Path.Combine(Root, "data")));
        Provider = services.BuildServiceProvider();
        using var db = Provider.GetRequiredService<IDbContextFactory<DesktopFlowDbContext>>().CreateDbContext();
        db.Database.EnsureCreated();
    }

    public string Root { get; }

    public string Desktop { get; }

    public string Downloads { get; }

    public ServiceProvider Provider { get; }

    public async Task<AppPreferences> SaveWorkspaceAsync(Action<AppPreferences>? configure = null)
    {
        var store = Provider.GetRequiredService<ISettingsStore>();
        var preferences = await store.GetAsync(CancellationToken.None);
        preferences.DesktopPath = Desktop;
        preferences.DownloadsPath = Downloads;
        preferences.DesktopMappings = DefaultTaxonomy.CreateDesktopMappings();
        preferences.DownloadsMappings = DefaultTaxonomy.CreateDownloadsMappings();
        preferences.HasCompletedSetup = true;
        preferences.EnableUndoHistory = true;
        preferences.KeepShortcuts = true;
        preferences.ExcludedPatterns = ["desktop.ini", "keep-me.txt"];
        configure?.Invoke(preferences);
        await store.SaveAsync(preferences, CancellationToken.None);
        return preferences;
    }

    public void Dispose()
    {
        Provider.Dispose();
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
        catch (IOException)
        {
        }
    }
}
