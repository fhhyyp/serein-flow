
using Serein.DbSql;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json;
using SqlSugar;
using System.Data;
using System.Linq.Expressions;
using Serein.Tool;

namespace Serein.Helper
{

    // public class RepositoryBase<TEntity> : DataBase, IRepositoryBase<TEntity> where TEntity : class, new()
    public class RepositoryBase<TEntity> : IRepositoryBase<TEntity> where TEntity : class, new()
    {
        public bool isHaveErr;

        public string ErrMsg = "";

        public string filterName = "SubSystemName";
        ~RepositoryBase()
        {
            DBSync.ReSetCrudDb();
        }
        public RepositoryBase()
        {
        }
        /// <summary>
        /// 是否优先使用本地数据库
        /// </summary>
        public bool IsUseLoaclDB = false;


        #region 数据库操作 泛型抽象方法

        #region 优先查询 主数据库

        /// <summary>
        /// 无状态数据操作（查询）泛型抽象方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        /// <exception cref="DBSyncException"></exception>
        public virtual T SyncExecuteRead<T>(Func<SqlSugarClient, T> func)
        {
            var syncSqlConfig = DBSync.GetSyncSqlConfig(); // 基类获取数据库配置
            if (IsUseLoaclDB)
            {
                var secondaryDB = syncSqlConfig.GetSecondaryDB();
                return func.Invoke(secondaryDB); // 尝试查询本地数据库
            }


            if (syncSqlConfig.GetNetworkState()) // 网络检测
            {
                try
                {
                    var primaryDB = syncSqlConfig.GetPrimaryDB();
                    if (primaryDB != null) 
                    {
                        return func.Invoke(primaryDB); // 尝试查询本地数据库
                    }
                    else
                    {
                        Console.WriteLine("远程数据库不可用");
                    }
                }
                catch(Exception ex)
                {
                    DBSync.SetIsNeedSyncData(true); // 网络不可达
                    Console.WriteLine(ex.ToString());
                }
            }

            try
            {
                var secondaryDB = syncSqlConfig.GetSecondaryDB();
                return func.Invoke(secondaryDB); // 尝试查询本地数据库
            }
            catch
            {
                throw new DBSyncException(DBSyncExType.CrudError, $"主从数据库不可用。\r\n {syncSqlConfig.ToString()} ");
            }
        }
        /// <summary>
        /// 无状态数据操作（查询）泛型抽象方法
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <returns></returns>
        /// <exception cref="DBSyncException"></exception>
        public virtual T SyncExecuteRead<T>(Func<SimpleClient<TEntity>, T> func)
        {
            var syncSqlConfig = DBSync.GetSyncSqlConfig(); // 基类获取数据库配置

            if (IsUseLoaclDB)
            {
                var secondaryDB = syncSqlConfig.GetSecondaryDB().GetSimpleClient<TEntity>();
                return func.Invoke(secondaryDB); // 尝试查询本地数据库
            }

            if (syncSqlConfig.GetNetworkState()) // 网络检测
            {
                try
                {
                    var primaryDB = syncSqlConfig.GetPrimaryDB()?.GetSimpleClient<TEntity>();
                    if (primaryDB != null)
                    {
                        return func.Invoke(primaryDB); // 尝试查询远程数据库
                    }
                    else
                    {
                        Console.WriteLine("远程数据库不可用");
                    }
                }
                catch (Exception ex)
                {
                    DBSync.SetIsNeedSyncData(true); // 网络不可达
                    Console.WriteLine(ex.ToString());
                }
            }

            try
            {
                var secondaryDB = syncSqlConfig.GetSecondaryDB().GetSimpleClient<TEntity>();
                return func.Invoke(secondaryDB); // 尝试查询本地数据库
            }
            catch
            {
                throw new DBSyncException(DBSyncExType.CrudError, $"主从数据库不可用。\r\n {syncSqlConfig.ToString()} ");
            }
        }

        #endregion


        #region 优先查询 从数据库 （已注释）
        /* /// <summary>
         /// 无状态数据操作（查询）泛型抽象方法
         /// </summary>
         /// <typeparam name="T"></typeparam>
         /// <param name="func"></param>
         /// <returns></returns>
         /// <exception cref="DBSyncException"></exception>
         public virtual T ExecuteSyncOperation<T>(Func<SqlSugarClient, T> func)
         {
             DBSync.SyncEvent.Wait();
             var secondaryDB = SyncSqlConfig.GetSecondaryDB();

             try
             {
                 return func.Invoke(secondaryDB); // 优先尝试查询本地数据库
             }
             catch
             {
                 try
                 {
                     var primaryDB = SyncSqlConfig.GetPrimaryDB();
                     if (primaryDB != null)
                     {
                         if (SyncSqlConfig.GetNetworkState()) // 网络检测
                         {
                             DBSync.SyncEvent.Wait();
                             return func.Invoke(primaryDB); // 尝试查询远程数据库
                         }
                         else
                         {
                             throw new DBSyncException(DBSyncExType.CrudError, "网络不可达，无法查询远程数据库。");
                         }
                     }
                     else
                     {
                         throw new DBSyncException(DBSyncExType.CrudError, "远程数据库不可用。");
                     }
                 }
                 catch
                 {
                     throw new DBSyncException(DBSyncExType.CrudError, $"远程数据库查询失败。\r\n {SyncSqlConfig.ToString()} ");
                 }
             }
         }

         /// <summary>
         /// 无状态数据操作（查询）泛型抽象方法
         /// </summary>
         /// <typeparam name="T"></typeparam>
         /// <param name="func"></param>
         /// <returns></returns>
         /// <exception cref="DBSyncException"></exception>
         public virtual T ExecuteSyncOperation<T>(Func<SimpleClient<TEntity>, T> func)
         {
             DBSync.SyncEvent.Wait();

             var secondaryDB = SyncSqlConfig.GetSecondaryDB().GetSimpleClient<TEntity>();

             try
             {
                 return func.Invoke(secondaryDB); // 优先尝试查询本地数据库
             }
             catch
             {
                 // 本地数据库查询失败，尝试远程数据库
                 try
                 {
                     var primaryDB = SyncSqlConfig.GetPrimaryDB()?.GetSimpleClient<TEntity>();
                     if (primaryDB != null)
                     {
                         if (SyncSqlConfig.GetNetworkState()) // 网络检测
                         {
                             DBSync.SyncEvent.Wait();
                             return func.Invoke(primaryDB); // 尝试查询远程数据库
                         }
                         else
                         {
                             throw new DBSyncException(DBSyncExType.CrudError, "网络不可达，无法查询远程数据库。");
                         }
                     }
                     else
                     {
                         throw new DBSyncException(DBSyncExType.CrudError, "远程数据库不可用。");
                     }
                 }
                 catch
                 {
                     throw new DBSyncException(DBSyncExType.CrudError, $"远程数据库查询失败。\r\n {SyncSqlConfig.ToString()} ");
                 }
             }
         }
        */
        #endregion

        #region 增加、更新、删除 操作泛型方法
        /// <summary>
        /// 有状态数据操作（更新、增加、删除）泛型抽象方法，优先操作本地数据库，操作远程数据库失败时调用DBSync.SetIsNeedSyncData(true);
        /// </summary>
        /// <typeparam name="TResult"></typeparam>
        /// <param name="primaryFunc"></param>
        /// <param name="secondaryFunc"></param>
        /// <returns></returns>
        /// <exception cref="DBSyncException"></exception>
        public virtual T SyncExecuteCUD<T>(Func<SqlSugarClient, T> func)
        {
            var syncSqlConfig = DBSync.GetSyncSqlConfig(); // 基类获取数据库配置
            var secondaryDB = syncSqlConfig.GetSecondaryDB();
            try
            {
                var secondaryResult = func.Invoke(secondaryDB); // 本地数据库操作
                if (IsUseLoaclDB)
                {
                    return secondaryResult;
                }
                if (syncSqlConfig.GetNetworkState()) // 网络检测
                {
                    var primaryDB = syncSqlConfig.GetPrimaryDB();
                    if(primaryDB != null)
                    {
                        var primaryResult = func.Invoke(primaryDB); // 远程数据库操作
                        return primaryResult;
                    }
                    else
                    {
                        Console.WriteLine("远程数据库不可用");
                    }
                }
                return secondaryResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine("主从数据库不可用：" + ex.ToString());
                DBSync.SetIsNeedSyncData(true);
                throw new DBSyncException(DBSyncExType.CrudError, $"主从数据库不可用。\r\n {syncSqlConfig.ToString()} ");
            }
        }

        public virtual T SyncExecuteCUD<T>(Func<SimpleClient<TEntity>, T> func)
        {
            var syncSqlConfig = DBSync.GetSyncSqlConfig(); // 基类获取数据库配置
            var secondaryDB = syncSqlConfig.GetSecondaryDB().GetSimpleClient<TEntity>();

            try
            {
                var secondaryResult = func.Invoke(secondaryDB); // 本地数据库操作
                if (IsUseLoaclDB)
                {
                    return secondaryResult;
                }
                if (syncSqlConfig.GetNetworkState()) // 网络检测
                {
                    var primaryDB = syncSqlConfig.GetPrimaryDB().GetSimpleClient<TEntity>();
                    if(primaryDB != null)
                    {
                        var primaryResult = func.Invoke(primaryDB); // 远程数据库操作
                        return primaryResult;
                    }
                    else
                    {
                        Console.WriteLine("远程数据库不可用");
                    }
                }
                return secondaryResult;
            }
            catch (Exception ex)
            {
                Console.WriteLine("主从数据库不可用：" + ex.ToString());
                DBSync.SetIsNeedSyncData(true);
                throw new DBSyncException(DBSyncExType.CrudError, $"主从数据库不可用。\r\n {syncSqlConfig.ToString()} ");
            }
        }


        #endregion


        public TEntity SyncRead(Func<SqlSugarClient, TEntity> func)
        {
            return SyncExecuteRead(func);
        }

        public bool SyncRead(Func<SqlSugarClient, bool> func)
        {
            return SyncExecuteRead(func);
        }

        public List<T> SyncRead<T>(Func<SqlSugarClient, List<T>> func)
        {
            return SyncExecuteRead(func);
        }


        /// <summary>
        /// 查询返回实体
        /// </summary>
        public TEntity SyncRead(Func<SimpleClient<TEntity>, TEntity> func)
        {
            return SyncExecuteRead(func);
        }

        /// <summary>
        /// 查询返回实体列表
        /// </summary>
        public List<T> SyncRead<T>(Func<SimpleClient<TEntity>, List<T>> func)
        {
            return SyncExecuteRead(func);
        }

        public TEntity SyncCUD(Func<SqlSugarClient, TEntity> func)
        {
            return SyncExecuteCUD(func);
        }

        public int SyncCUD(Func<SqlSugarClient, int> func)
        {
            return SyncExecuteCUD(func);
        }

        public bool SyncCUD(Func<SqlSugarClient, bool> func)
        {
            return SyncExecuteCUD(func);
        }

        public TEntity SyncSimpleCUD(Func<SimpleClient<TEntity>, TEntity> func)
        {
            
            return SyncExecuteCUD(func);
        }

        public int SyncSimpleCUD(Func<SimpleClient<TEntity>, int> func)
        {
            return SyncExecuteCUD(func);
        }

        public bool SyncSimpleCUD(Func<SimpleClient<TEntity>, bool> func)
        {
            return SyncExecuteCUD(func);
        }


        #endregion




        public virtual TEntity GetModelByID(dynamic ID)
        {
            return SyncRead(db => db.GetById(ID));
        }

        public virtual TEntity GetModel(Expression<Func<TEntity, bool>> where)
        {
            try
            {
                return SyncRead(db => db.Queryable<TEntity>().Where(where).First()); //db.GetSingle(where));
                // GetSingle结果不能大于1
            }
            catch (Exception ex)
            {

                isHaveErr = true;
                ErrMsg = ex.Message;

                return null;

            }
        }


        public virtual int Add(TEntity model)
        {
            try
            {
                return SyncCUD(db => db.Insertable(model).ExecuteCommand());
            }
            catch (Exception ex)
            {
                isHaveErr = true;
                ErrMsg = ex.Message;
                return 0;
            }
        }


        public virtual int AddAndReturnIndex(TEntity model)
        {
            try
            {
                return SyncCUD(db => db.Insertable(model).ExecuteReturnIdentity());
            }
            catch (Exception ex)
            {
                isHaveErr = true;
                ErrMsg = ex.Message;
                return 0;
            }
        }


        public virtual bool Exist(Expression<Func<TEntity, bool>> where)
        {
            try
            {
                return SyncRead(db => db.Queryable<TEntity>().Where(where).Take(1).Any());
            }
            catch (Exception ex)
            {
                isHaveErr = true;
                ErrMsg = ex.Message;
                return false;
            }
        }


        public int AddOrUpdate(TEntity model, string keyValue)
        {
            if (keyValue == "")
            {
                try
                {
                    return SyncCUD(db => db.Insertable(model).ExecuteCommand());
                }
                catch (Exception ex)
                {
                    isHaveErr = true;
                    ErrMsg = ex.Message;
                    return 0;
                }
            }
            return SyncCUD(db => db.Updateable(model).ExecuteCommand());
        }


        public virtual int Update(TEntity model)
        {
            return SyncCUD(db => db.Updateable(model).ExecuteCommand());
        }


        public virtual int UpdateColumns(TEntity model, Expression<Func<TEntity, object>> expression)
        {
            //DatabaseSync.StartcaControls();
            try
            {
                return SyncCUD(db => db.Updateable(model).UpdateColumns(expression).ExecuteCommand());
            }
            catch (Exception ex)
            {
                isHaveErr = true;
                ErrMsg = ex.Message;
                return 0;
            }
        }


        public virtual bool DeleteByID(dynamic ID)
        {
            
            //SyncCUD(db => db.Updateable<TEntity>().RemoveDataCache().ExecuteCommand());
            return SyncSimpleCUD(db => (bool)db.DeleteById(ID));
        }


        public virtual bool Delete(Expression<Func<TEntity, bool>> where)
        {
            return SyncSimpleCUD(db => db.Delete(where));
        }



        public virtual string GetPageList(Pagination pagination, Expression<Func<TEntity, bool>> where = null)

        {
            //DatabaseSync.StartcaControls();
            return new
            {
                rows = GetList(pagination, where),
                total = pagination.total,
                page = pagination.page,
                records = pagination.records
            }.ToJson();
        }


        public virtual TEntity GetSingle(Expression<Func<TEntity, bool>> expression)
        {
            //DatabaseSync.StartcaControls();
            return SyncRead(db => db.Queryable<TEntity>().Filter(filterName, isDisabledGobalFilter: true).Single(expression));
        }



        public virtual List<TEntity> GetTop(int Top, Expression<Func<TEntity, object>> expression, OrderByType _OrderByType = OrderByType.Asc, Expression<Func<TEntity, bool>> where = null, string selstr = "*")

        {
            return SyncRead(db => db.Queryable<TEntity>().Select(selstr).WhereIF(where != null, where)
                        .Take(Top)
                        .OrderBy(expression, _OrderByType)
                        .Filter(filterName, isDisabledGobalFilter: true)
                        .ToList());
        }

        /// <summary>
        /// 排序表达式所用的键，排序方式，搜索条件
        /// </summary>
        /// <param name="OrderExpression"></param>
        /// <param name="_OrderByType"></param>
        /// <param name="where"></param>
        /// <returns></returns>

        public virtual TEntity GetFirst(Expression<Func<TEntity, object>> OrderExpression, OrderByType _OrderByType = OrderByType.Asc, Expression<Func<TEntity, bool>> where = null)

        {
            return SyncRead(db => db.Queryable<TEntity>().Filter(filterName, isDisabledGobalFilter: true).WhereIF(where != null, where)
                        .OrderBy(OrderExpression, _OrderByType)
                        .First());
        }


        public virtual List<TEntity> GetList(Pagination pagination, Expression<Func<TEntity, bool>> where = null)

        {
            int totalNumber = 0;
            List<TEntity> result = SyncRead(db => db.Queryable<TEntity>().WhereIF(where != null, where).OrderBy(pagination.sidx + " " + pagination.sord)
                        .Filter(filterName, isDisabledGobalFilter: true)
                        .ToPageList(pagination.page, pagination.rows, ref totalNumber));
            pagination.records = totalNumber;
            return result;
        }



        public virtual List<TEntity> GetList(Expression<Func<TEntity, bool>> where = null)

        {
            return SyncRead(db => db.Queryable<TEntity>().WhereIF(where != null, where).Filter(filterName, isDisabledGobalFilter: true)
                        .ToList());
        }

        public virtual List<TEntity> GetList()
        {
            return SyncRead(db => db.Queryable<TEntity>().ToList());
        }




        public virtual DataTable GetDataTable(Expression<Func<TEntity, bool>> where = null, Pagination pagination = null)


        {
            if (pagination != null)
            {
                return DataHelper.ListToDataTable(GetList(pagination, where));
            }
            return DataHelper.ListToDataTable(GetList(where));
        }

        public virtual void UseFilter(SqlFilterItem item)
        {
            SyncCUD(db =>
            {
                db.QueryFilter.Remove(item.FilterName);
                db.QueryFilter.Add(item);
                return 0;
            });
            filterName = item.FilterName;
        }


        public virtual void ClearFilter()
        {
            SyncCUD(db =>
            {
                db.QueryFilter.Clear();
                return 0;
            });


            filterName = null;

        }








        /* public void ReSetConnStr(string constr, SqlSugar.DbType _dbtype)
         {
             db = DBHelper.CreateDB(constr, _dbtype);
             Sclient = db.GetSimpleClient<TEntity>();
         }


         public virtual TEntity GetModelByID(dynamic ID)
         {
             return Sclient.GetById(ID);
         }


         public virtual TEntity GetModel(Expression<Func<TEntity, bool>> where)
         {
             try
             {
                 return Sclient.GetSingle(where);
             }
             catch (Exception ex)
             {
                 isHaveErr = true;
                 ErrMsg = ex.Message;
                 return null;
             }
         }


         public virtual int Add(TEntity model)
         {
             try
             {
                 return db.Insertable(model).ExecuteCommand();
             }
             catch (Exception ex)
             {
                 isHaveErr = true;
                 ErrMsg = ex.Message;
                 return 0;
             }
         }


         public virtual int AddAndReturnIndex(TEntity model)
         {
             try
             {
                 return db.Insertable(model).ExecuteReturnIdentity();
             }
             catch (Exception ex)
             {
                 isHaveErr = true;
                 ErrMsg = ex.Message;
                 return 0;
             }
         }


         public virtual bool Exist(Expression<Func<TEntity, bool>> where)
         {
             return db.Queryable<TEntity>().Where(where).Take(1).Any();
         }


         public int AddOrUpdate(TEntity model, string Keyvale)
         {
             if (Keyvale == "")
             {
                 return db.Insertable(model).ExecuteCommand();
             }
             return db.Updateable(model).ExecuteCommand();
         }


         public virtual int Update(TEntity model)
         {
             return db.Updateable(model).ExecuteCommand();
         }


         public virtual int UpdateColumns(TEntity model, Expression<Func<TEntity, object>> expression)
         {
             return db.Updateable(model).UpdateColumns(expression).ExecuteCommand();
         }


         public virtual bool DeleteByID(dynamic ID)
         {
             db.Updateable<TEntity>().RemoveDataCache().ExecuteCommand();
             return Sclient.DeleteById(ID);
         }


         public virtual bool Delete(Expression<Func<TEntity, bool>> where)
         {
             return Sclient.Delete(where);
         }


         public virtual string GetPageList(Pagination pagination, Expression<Func<TEntity, bool>> where = null)
         {
             return new
             {
                 rows = GetList(pagination, where),
                 total = pagination.total,
                 page = pagination.page,
                 records = pagination.records
             }.ToJson();
         }


         public virtual TEntity GetSingle(Expression<Func<TEntity, bool>> expression)
         {
             return db.Queryable<TEntity>().Filter(filterName, isDisabledGobalFilter: true).Single(expression);
         }


         public virtual List<TEntity> GetTop(int Top, Expression<Func<TEntity, object>> expression, OrderByType _OrderByType = OrderByType.Asc, Expression<Func<TEntity, bool>> where = null, string selstr = "*")
         {
             return db.Queryable<TEntity>().Select(selstr).WhereIF(where != null, where)
                 .Take(Top)
                 .OrderBy(expression, _OrderByType)
                 .Filter(filterName, isDisabledGobalFilter: true)
                 .ToList();
         }


         public virtual TEntity GetFirst(Expression<Func<TEntity, object>> OrderExpression, OrderByType _OrderByType = OrderByType.Asc, Expression<Func<TEntity, bool>> where = null)
         {
             return db.Queryable<TEntity>().Filter(filterName, isDisabledGobalFilter: true).WhereIF(where != null, where)
                 .OrderBy(OrderExpression, _OrderByType)
                 .First();
         }

         public virtual List<TEntity> GetList(Pagination pagination, Expression<Func<TEntity, bool>> where = null)
         {
             int totalNumber = 0;
             List<TEntity> result = db.Queryable<TEntity>().WhereIF(where != null, where).OrderBy(pagination.sidx + " " + pagination.sord)
                 .Filter(filterName, isDisabledGobalFilter: true)
                 .ToPageList(pagination.page, pagination.rows, ref totalNumber);
             pagination.records = totalNumber;
             return result;
         }


         public virtual List<TEntity> GetList(Expression<Func<TEntity, bool>> where = null)
         {
             return db.Queryable<TEntity>().WhereIF(where != null, where).Filter(filterName, isDisabledGobalFilter: true)
                 .ToList();
         }

         public virtual List<TEntity> GetList()
         {
             return db.Queryable<TEntity>().ToList();
         }


         public virtual DataTable GetDataTable(Expression<Func<TEntity, bool>> where = null, Pagination pagination = null)
         {
             if (pagination != null)
             {
                 return DataHelper.ListToDataTable(GetList(pagination, where));
             }
             return DataHelper.ListToDataTable(GetList(where));
         }

         public virtual void UseFilter(SqlFilterItem item)
         {
             db.QueryFilter.Remove(item.FilterName);
             db.QueryFilter.Add(item);
             filterName = item.FilterName;
         }


         public virtual void ClearFilter()
         {
             db.QueryFilter.Clear();
             filterName = null;
         }

         public void BeginTran()
         {
             db.Ado.BeginTran();
         }

         public void CommitTran()
         {
             db.Ado.CommitTran();
         }

         public void RollbackTran()
         {
             db.Ado.RollbackTran();
         }*/
    }
    public class Pagination
    {
        /// <summary>
        /// 每页行数
        /// </summary>
        public int rows { get; set; }

        /// <summary>
        /// 当前页
        /// </summary>
        public int page { get; set; }

        /// <summary>
        /// /排序列
        /// </summary>

        public string sidx { get; set; }


        /// <summary>
        /// 排序类型
        /// </summary>

        public string sord { get; set; }


        /// <summary>
        /// 总记录数
        /// </summary>
        public int records { get; set; }

        /// <summary>
        /// 总页数
        /// </summary>
        public int total
        {
            get
            {
                if (records > 0)
                {
                    if (records % rows != 0)
                    {
                        return records / rows + 1;
                    }

                    return records / rows;
                }
                return 0;
            }
        }
    }


}
