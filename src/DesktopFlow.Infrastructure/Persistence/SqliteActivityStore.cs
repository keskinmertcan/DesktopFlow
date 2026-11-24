using DesktopFlow.Core.Abstractions;
using DesktopFlow.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DesktopFlow.Infrastructure.Persistence;

public sealed class SqliteActivityStore : IActivityStore
{
    private readonly IDbContextFactory<DesktopFlowDbContext> _factory;
    private readonly SqliteGate _gate;

    public SqliteActivityStore(IDbContextFactory<DesktopFlowDbContext> factory, SqliteGate gate)
    {
        _factory = factory;
        _gate = gate;
    }

    public async Task<ActivityRecord> AddAsync(ActivityRecord record, CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = new ActivityEntity();
            EntityMapping.CopyTo(record, entity);
            db.Activities.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            record.Id = entity.Id;
            return record;
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task UpdateAsync(ActivityRecord record, CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await db.Activities.FirstOrDefaultAsync(item => item.Id == record.Id, cancellationToken);
            if (entity is null)
            {
                return;
            }

            EntityMapping.CopyTo(record, entity);
            await db.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<ActivityRecord?> GetAsync(long id, CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var entity = await db.Activities.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
            return entity is null ? null : EntityMapping.ToModel(entity);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityRecord>> QueryAsync(ActivityQuery query, CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            IQueryable<ActivityEntity> rows = db.Activities.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(query.FileNameContains))
            {
                var text = query.FileNameContains.Trim();
                rows = rows.Where(row => row.FileName.Contains(text));
            }

            if (!string.IsNullOrWhiteSpace(query.CategoryKey))
            {
                rows = rows.Where(row => row.CategoryKey == query.CategoryKey);
            }

            if (query.Kind is not null)
            {
                var kind = query.Kind.Value.ToString();
                rows = rows.Where(row => row.Kind == kind);
            }

            if (query.Status is not null)
            {
                var status = query.Status.Value.ToString();
                rows = rows.Where(row => row.Status == status);
            }

            if (query.From is not null)
            {
                var from = query.From.Value.UtcDateTime;
                rows = rows.Where(row => row.TimestampUtc >= from);
            }

            if (query.To is not null)
            {
                var to = query.To.Value.UtcDateTime;
                rows = rows.Where(row => row.TimestampUtc <= to);
            }

            var limit = Math.Clamp(query.Limit, 1, 500);
            var entities = await rows.OrderByDescending(row => row.TimestampUtc).Take(limit).ToListAsync(cancellationToken);
            return entities.Select(EntityMapping.ToModel).ToList();
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<int> CountSucceededMovesAsync(CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var kind = ActivityKind.Move.ToString();
            var status = ActivityStatus.Succeeded.ToString();
            return await db.Activities.CountAsync(
                row => row.Kind == kind && row.Status == status && !row.IsUndone,
                cancellationToken);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<DateTimeOffset?> LastSucceededMoveAsync(CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var kind = ActivityKind.Move.ToString();
            var status = ActivityStatus.Succeeded.ToString();
            var latest = await db.Activities
                .Where(row => row.Kind == kind && row.Status == status)
                .OrderByDescending(row => row.TimestampUtc)
                .Select(row => (DateTime?)row.TimestampUtc)
                .FirstOrDefaultAsync(cancellationToken);
            return latest is null
                ? null
                : new DateTimeOffset(DateTime.SpecifyKind(latest.Value, DateTimeKind.Utc));
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetPendingAsync(CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            var status = ActivityStatus.Pending.ToString();
            var entities = await db.Activities.Where(row => row.Status == status).ToListAsync(cancellationToken);
            return entities.Select(EntityMapping.ToModel).ToList();
        }
        finally
        {
            _gate.Lock.Release();
        }
    }

    public async Task<IReadOnlyList<ActivityRecord>> GetRecentAsync(int count, CancellationToken cancellationToken)
    {
        return await QueryAsync(new ActivityQuery { Limit = count }, cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken)
    {
        await _gate.Lock.WaitAsync(cancellationToken);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(cancellationToken);
            return await db.Activities
                .Where(row => row.CategoryKey != null && row.CategoryKey != "")
                .Select(row => row.CategoryKey!)
                .Distinct()
                .OrderBy(value => value)
                .ToListAsync(cancellationToken);
        }
        finally
        {
            _gate.Lock.Release();
        }
    }
}
