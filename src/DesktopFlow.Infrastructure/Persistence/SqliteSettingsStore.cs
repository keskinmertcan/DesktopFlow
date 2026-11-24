using System.Text.Json;
using System.Text.Json.Serialization;
using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DesktopFlow.Infrastructure.Persistence;

public sealed class SqliteSettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IDbContextFactory<DesktopFlowDbContext> _factory;
    private readonly SqliteGate _gate;
    private readonly object _cacheGate = new();
    private AppPreferences _current = new();

    public SqliteSettingsStore(IDbContextFactory<DesktopFlowDbContext> factory, SqliteGate gate)
    {
        _factory = factory;
        _gate = gate;
    }

    public AppPreferences Current
    {
        get
        {
            lock (_cacheGate)
            {
                return Clone(_current);
            }
        }
    }

    public async Task<AppPreferences> GetAsync(CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var row = await db.Preferences.FirstOrDefaultAsync(item => item.Id == 1, cancellationToken);
            var preferences = row is null
                ? new AppPreferences()
                : JsonSerializer.Deserialize<AppPreferences>(row.Json, Options) ?? new AppPreferences();
            lock (_cacheGate)
            {
                _current = Clone(preferences);
            }

            return Clone(preferences);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var row = await db.Preferences.FirstOrDefaultAsync(item => item.Id == 1, cancellationToken);
            if (row is null)
            {
                row = new PreferenceEntity { Id = 1 };
                db.Preferences.Add(row);
            }

            row.Json = JsonSerializer.Serialize(preferences, Options);
            await db.SaveChangesAsync(cancellationToken);
            lock (_cacheGate)
            {
                _current = Clone(preferences);
            }
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    private static AppPreferences Clone(AppPreferences preferences)
    {
        var json = JsonSerializer.Serialize(preferences, Options);
        return JsonSerializer.Deserialize<AppPreferences>(json, Options) ?? new AppPreferences();
    }
}
