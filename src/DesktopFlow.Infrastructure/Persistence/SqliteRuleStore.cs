using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DesktopFlow.Infrastructure.Persistence;

public sealed class SqliteRuleStore : IRuleStore
{
    private readonly IDbContextFactory<DesktopFlowDbContext> _factory;
    private readonly SqliteGate _gate;

    public SqliteRuleStore(IDbContextFactory<DesktopFlowDbContext> factory, SqliteGate gate)
    {
        _factory = factory;
        _gate = gate;
    }

    public async Task<IReadOnlyList<AutomationRule>> GetAllAsync(CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entities = await db.Rules.AsNoTracking()
                .OrderBy(row => row.Priority)
                .ThenBy(row => row.Name)
                .ToListAsync(cancellationToken);
            return entities.Select(EntityMapping.ToModel).ToList();
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<IReadOnlyList<AutomationRule>> GetEnabledAsync(MonitorScope scope, CancellationToken cancellationToken)
    {
        var scopeName = scope.ToString();
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entities = await db.Rules.AsNoTracking()
                .Where(row => row.IsEnabled && row.Scope == scopeName)
                .OrderBy(row => row.Priority)
                .ToListAsync(cancellationToken);
            return entities.Select(EntityMapping.ToModel).ToList();
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task SaveAsync(AutomationRule rule, CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await db.Rules.FirstOrDefaultAsync(row => row.Id == rule.Id, cancellationToken);
            if (entity is null)
            {
                db.Rules.Add(EntityMapping.ToEntity(rule));
            }
            else
            {
                EntityMapping.CopyTo(rule, entity);
            }

            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await db.Rules.FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
            if (entity is null)
            {
                return;
            }

            db.Rules.Remove(entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }
}
