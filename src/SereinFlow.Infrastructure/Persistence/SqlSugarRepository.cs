using System.Linq.Expressions;
using SqlSugar;
using SereinFlow.Application.Persistence;

namespace SereinFlow.Infrastructure.Persistence;

internal sealed class SqlSugarRepository<TEntity> : IRepository<TEntity>
    where TEntity : class, new()
{
    private readonly ISqlSugarClient _database;

    public SqlSugarRepository(ISqlSugarClient database)
    {
        _database = database;
    }

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.Queryable<TEntity>().InSingleAsync(id);
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = _database.Queryable<TEntity>();
        if (predicate is not null)
            query = query.Where(predicate);
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        await _database.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.Updateable(entity).ExecuteCommandHasChangeAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await _database.Deleteable<TEntity>().In(id).ExecuteCommandHasChangeAsync();
    }
}

internal sealed class SqlSugarUnitOfWork : IUnitOfWork
{
    private readonly ISqlSugarClient _database;

    public SqlSugarUnitOfWork(ISqlSugarClient database) => _database = database;

    public async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        cancellationToken.ThrowIfCancellationRequested();
        _database.Ado.BeginTran();
        try
        {
            var result = await action(cancellationToken);
            _database.Ado.CommitTran();
            return result;
        }
        catch
        {
            _database.Ado.RollbackTran();
            throw;
        }
    }
}
