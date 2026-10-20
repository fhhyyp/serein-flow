using System.Linq.Expressions;
using SqlSugar;
using SereinFlow.Application.Persistence;

namespace SereinFlow.Infrastructure.Persistence;
internal sealed class SqlSugarRepository<TEntity> : IRepository<TEntity> where TEntity : class, new()
{
    private readonly ISqlSugarClient _database;
    public SqlSugarRepository(ISqlSugarClient database)
    {
        _database = database;
    }

    public async Task<TEntity?> GetByIdAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var queryableResult = _database.Queryable<TEntity>();
        var inSingleAsyncResult = await queryableResult.InSingleAsync(id);
        return inSingleAsyncResult;
    }

    public async Task<IReadOnlyList<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var query = _database.Queryable<TEntity>();
        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        var toListAsyncResult = await query.ToListAsync(cancellationToken);
        return toListAsyncResult;
    }

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        var insertableResult = _database.Insertable(entity);
        await insertableResult.ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    public async Task<bool> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        cancellationToken.ThrowIfCancellationRequested();
        var updateableResult = _database.Updateable(entity);
        var executeCommandHasChangeAsyncResult = await updateableResult.ExecuteCommandHasChangeAsync(cancellationToken);
        return executeCommandHasChangeAsyncResult;
    }

    public async Task<bool> DeleteAsync(object id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var deleteableResult = _database.Deleteable<TEntity>();
        var inResult = deleteableResult.In(id);
        var executeCommandHasChangeAsyncResult1 = await inResult.ExecuteCommandHasChangeAsync();
        return executeCommandHasChangeAsyncResult1;
    }
}

internal sealed class SqlSugarUnitOfWork : IUnitOfWork
{
    private readonly ISqlSugarClient _database;
    public SqlSugarUnitOfWork(ISqlSugarClient database)
    {
        _database = database;
    }

    public async Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> action, CancellationToken cancellationToken = default)
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
