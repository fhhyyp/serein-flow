using System.Linq.Expressions;

namespace Serein.DbSql
{
    public interface IRepositoryBase<TEntity> where TEntity : class, new()
    {
        TEntity GetModelByID(dynamic ID);

        int Add(TEntity Model);

        int Update(TEntity Model);

        bool DeleteByID(dynamic ID);

        bool Delete(Expression<Func<TEntity, bool>> where);

        int UpdateColumns(TEntity model, Expression<Func<TEntity, object>> expression);
    }
}
