using SqlSugar;
using System.ComponentModel;
using System.Net.Sockets;
using System.Reflection;

namespace Serein.DbSql
{
    public enum DBSyncStart
    {
        /// <summary>
        /// 无需同步
        /// </summary>
        [Description("无需同步")]
        NotNeed,
        /// <summary>
        /// 同步成功
        /// </summary>
        [Description("同步成功")]
        SyncSuccess,
        /// <summary>
        /// 同步失败
        /// </summary>
        [Description("同步失败")]
        SyncFailure,
        /// <summary>
        /// 连接异常
        /// </summary>
        [Description("配置/连接异常")]
        NetworkError,
        /// <summary>
        /// 没有同步事件
        /// </summary>
        [Description("没有同步事件，请使用 DBSync.SetSyncDataEvent() 方法设置同步事件")]
        NoEvent,
    }
    public enum DBSyncExType
    {
        [Description("连接异常")]
        ConnectError,
        [Description("读写异常")]
        CrudError,
        [Description("同步异常")]
        SyncError,
    }

    public class DBSyncConfig
    {
        public DBSyncConfig(ConnectionConfig primaryDBConfig,
                            ConnectionConfig secondaryDBConfig)
        {
            PrimaryDBConfig = primaryDBConfig;
            SecondaryDBConfig = secondaryDBConfig;
        }
        /// <summary>
        /// 主数据库IP
        /// </summary>
        //private string Host { get; }
        /// <summary>
        /// 主数据库端口
        /// </summary>
        //private int Port { get; }
        /// <summary>
        /// 主数据库配置
        /// </summary>
        private ConnectionConfig PrimaryDBConfig { get; }
        /// <summary>
        /// 从数据库配置
        /// </summary>
        private ConnectionConfig SecondaryDBConfig { get; }

        public override string ToString()
        {
            return $"[主数据库配置]{PrimaryDBConfig.ConnectionString}" + Environment.NewLine +
                   $"[从数据库配置]{SecondaryDBConfig.ConnectionString}" + Environment.NewLine;
        }

        /// <summary>
        /// 检查网络状态
        /// </summary>
        /// <returns></returns>
        public bool GetNetworkState()
        {
            var isOpen = DBSync.IsPortOpen(); // 数据库基类获取网络状态
            if (!isOpen)
            {
                DBSync.SetIsNeedSyncData(true); // 远程数据库查询失败，尝试本地数据库
            }
            return isOpen;
        }

        /// <summary>
        /// 返回从数据库
        /// </summary>
        /// <returns></returns>
        public SqlSugarClient GetSecondaryDB()
        {
            DBSync.SyncEvent.Wait();
            return new SqlSugarClient(SecondaryDBConfig);
        }

        /// <summary>
        /// 返回主数据库
        /// </summary>
        /// <returns></returns>
        /// <exception cref="DBSyncException"></exception>
        public SqlSugarClient GetPrimaryDB()
        {
            try
            {
                // 等待同步事件
                DBSync.SyncEvent.Wait();
                // 检查主数据库连接状态
                if (!DBSync.IsPortOpen()) // 返回主数据库检测网络状态
                {
                    // Console.WriteLine($"主数据库无法连接，IP:{IP},端口:{Port}");
                    DBSync.SetIsNeedSyncData(true); // 网络不可达

                    return null;

                }

                // 检查是否需要同步数据
                /*if (DBSync.GetIsNeedSyncData())
                {
                    var syncState = DBSync.StartSyncDataBase();
                    if (syncState != DBSyncStart.SyncSuccess && syncState != DBSyncStart.NotNeed)
                    {
                        // Console.WriteLine($"获取读写客户端前，尝试同步时发生异常：{DBSync.GetDescription(syncState)}");
                        return null;
                    }
                }*/

                // 返回主数据库客户端
                return new SqlSugarClient(PrimaryDBConfig);
            }
            catch //  (Exception ex)
            {
                // Console.WriteLine($"发生异常：{ex.Message}");

                return null;

            }
        }


    }

    /// <summary>
    /// 数据库同步异常
    /// </summary>
    public class DBSyncException : Exception
    {
        public DBSyncExType ExceptionType { get; private set; }

        public DBSyncException(DBSyncExType exceptionType)
        {
            ExceptionType = exceptionType;
        }

        public DBSyncException(DBSyncExType exceptionType, string message) : base(message)
        {
            ExceptionType = exceptionType;
        }

        public DBSyncException(DBSyncExType exceptionType, string message, Exception innerException) : base(message, innerException)
        {
            ExceptionType = exceptionType;
        }
        public override string ToString()
        {
            return $"异常： {ExceptionType}: {GetDescription(ExceptionType)}. Message: {Message}";
        }
        public static string GetDescription(DBSyncExType value)
        {

            FieldInfo field = value.GetType().GetField(value.ToString());



            DescriptionAttribute attribute = (DescriptionAttribute)field.GetCustomAttribute(typeof(DescriptionAttribute));


            return attribute == null ? value.ToString() : attribute.Description;
        }

    }

 
    /// <summary>
    /// 远程、本地数据库同步
    /// </summary>
    public static class DBSync
    {
        /// <summary>
        /// 主数据库配置
        /// </summary>

        private static ConnectionConfig PrimaryConfig { get; set; }

        /// <summary>
        /// 从数据库配置
        /// </summary>

        private static ConnectionConfig SecondaryConfig { get; set; }

        /// <summary>
        /// 主数据库IP
        /// </summary>

        private static string Host { get; set; }

        /// <summary>
        /// 主数据库端口
        /// </summary>
        private static int Port { get; set; }
        /// <summary>
        /// 同步数据事件（远程数据库，本地数据库，是否执行成功）
        /// </summary>

        private static Func<SqlSugarClient, SqlSugarClient, bool> SyncDataEvent { get; set; }


        private static Action<bool> StateChangeEvent { get; set; }

        /// <summary>
        /// 数据库设置锁
        /// </summary>
        //private static object DBSetLock { get; set; } = new object();
        /// <summary>
        /// 是否需要同步数据
        /// </summary>
        private static bool IsNeedSyncData { get; set; } = false;
        /// <summary>
        /// 等待次数（执行了多少次操作后才尝试进行同步，设置为0容易影响性能）
        /// </summary>
        private static int WaitCount { get; set; } = 10;

        /// <summary>
        /// 客户端获取计数
        /// </summary>
        private static int CrudDBGetCount { get; set; } = 0;
        /// <summary>
        /// 同步端获取计数
        /// </summary>
        private static int SyncDBGetCount { get; set; } = 0;


        //public static ManualResetEventSlim SyncEvent { get; } = new ManualResetEventSlim(true); // 同步事件
        /// <summary>
        /// 远程本地同步阻塞事件
        /// </summary>
        public static FifoManualResetEvent SyncEvent { get; } = new FifoManualResetEvent(true);
        /// <summary>
        /// 数据同步锁
        /// </summary>
        private static object SyncLock { get; } = new object();
        /// <summary>
        /// 是否需要同步数据读写锁
        /// </summary>
        private static readonly ReaderWriterLockSlim NeedSyncStateLock = new ReaderWriterLockSlim();

        /// <summary>
        /// 是否断开过,true=断开过，false=没有断开过
        /// 设置为 false 时自动检测网络情况，只有在网络正常的情况下才能成功设置为 true
        /// </summary>
        /// <param name="value"></param>
        public static void SetIsNeedSyncData(bool value)
        {
            if (value == IsNeedSyncData)
            {
                return;
            }
            //Console.WriteLine("变更数据库");
            // 写入锁
            NeedSyncStateLock.EnterWriteLock();
            try
            {
                if (value)
                {
                    IsNeedSyncData = true;
                    return;
                }
                IsNeedSyncData = !IsPortOpen(); // 变更 是否同步 属性时获取网络状态
            }
            finally
            {
                NeedSyncStateLock.ExitWriteLock();
                StateChangeEvent?.Invoke(IsNeedSyncData);
            }
        }
        public static bool GetIsNeedSyncData()
        {
            // 读取锁
            NeedSyncStateLock.EnterReadLock();
            try
            {
                return IsNeedSyncData; //是否需要同步数据
            }
            finally
            {
                NeedSyncStateLock.ExitReadLock();
            }
        }



        /// <summary>
        /// 配置主数据库
        /// </summary>
        public static void PrimaryConnect(DbType dbType, string host, int port, string dbName, string user, string password)
        {
            Host = host;
            Port = port;
            PrimaryConfig = GetConnectionConfig(dbType, host, port.ToString(), dbName, user, password);

            /*SyncEvent.Wait();

            if (true || IsPortOpen(host, port))
            {
                // 目标端口打通时才会更改数据库配置
                lock (DBSetLock)
                {
                    Host = host;
                    Port = port;
                    PrimaryConfig = GetConnectionConfig(dbType, host, port.ToString(), dbName, user, password);
                }
            }
            else
            {
                throw new DBSyncException(DBSyncExType.ConnectError, $"主数据库配置失败，无法连接，目标配置：IP:{host},端口:{port},目标库名:{dbName},账户:{user}");
            }*/
        }
        /// <summary>
        ///  配置从数据库
        /// </summary>
        public static void SecondaryConnect(DbType dbType, string host, int port, string dbName, string user, string password)
        {
            SecondaryConfig = GetConnectionConfig(dbType, host, port.ToString(), dbName, user, password);

            /*if (IsPortOpen(host, port))
            {
                lock (DBSetLock)
                {
                    SecondaryConfig = GetConnectionConfig(dbType, host, port.ToString(), dbName, user, password);
                }
            }
            else
            {
                throw new DBSyncException(DBSyncExType.ConnectError, $"从数据库配置失败，无法连接，目标配置：{host},端口:{port},目标库名:{dbName},账户:{user}");
            }*/
        }

        /// <summary>
        /// 尝试执行一次数据同步
        /// </summary>
        public static bool SyncData()
        {
            SetIsNeedSyncData(true);
            var state = StartSyncDataBase(true); // 手动同步
            return state == DBSyncStart.SyncSuccess || state == DBSyncStart.NotNeed;
        }



        /// <summary>
        ///  设置同步事件与等待次数。
        /// </summary>
        /// <param name="syncDataEvent">同步事件（需要手动同步数据）</param>
        /// <param name="waitCount">等待次数（执行了多少次操作后才尝试进行同步，设置为0容易影响性能）</param>
        public static void SetSyncEvent(Func<SqlSugarClient, SqlSugarClient, bool> syncDataEvent, int waitCount = 0)
        {
            SyncDataEvent = syncDataEvent;
            WaitCount = waitCount;
        }
        /// <summary>
        /// 设置状态变化事件
        /// </summary>
        /// <param name="stateChangeEvent"></param>
        /// <param name="isAtOnce"></param>
        public static void SetStateChangeEvent(Action<bool> stateChangeEvent)
        {
            StateChangeEvent = stateChangeEvent;
        }

        /// <summary>
        /// 获取数据库配置（不推荐使用在除了Repository的地方外部调用）
        /// </summary>
        /// <returns></returns>
        public static DBSyncConfig GetSyncSqlConfig()
        {
            /*SyncEvent.Wait();
            */

            if (GetIsNeedSyncData())
            {
                _ = Task.Run(() => StartSyncDataBase());  // new了一个RepositoryBase时尝试同步数据
            }

            lock (SyncLock)
            {
                CrudDBGetCount++;
                //Console.WriteLine($"获取客户端:{CrudDBGetCount}");
                return new DBSyncConfig(PrimaryConfig, SecondaryConfig);
            }
        }

        public static void ReSetCrudDb()
        {
            CrudDBGetCount--;
            Task.Run(() => StartSyncDataBase()); // 释放数据库连接时尝试同步数据

            /*if (GetIsNeedSyncData())
            {
                
            }*/
            // Console.WriteLine($"释放客户端:{CrudDBGetCount}");
        }

        public static DBSyncStart StartSyncDataBase(bool isAtOnce = false)
        {
            /*if (!isAtOnce && WaitCount > 0)
            {
                WaitCount--;
                return DBSyncStart.NotNeed;
            }*/

            SyncEvent.Reset(); // 锁定线程，保证只有一个线程进入该方法

            if (!GetIsNeedSyncData())
            {
                SyncEvent.Set();
                return DBSyncStart.NotNeed;
            }

            if (!IsPortOpen()) // 同步时获取网络状态
            {
                SetIsNeedSyncData(true);
                SyncEvent.Set();
                return DBSyncStart.NetworkError;
            }



            if (SyncDataEvent == null)
            {
                SyncEvent.Set();
                return DBSyncStart.NoEvent;
            }


            lock (SyncLock) // 同步锁，避免其它符合进入条件的线程执行多次同步
            {
                if (!GetIsNeedSyncData())
                {
                    SyncEvent.Set();
                    return DBSyncStart.NotNeed;
                }
                Console.WriteLine("网络检测OK，准备同步数据");
                try
                {
                    bool isSuccess = SyncDataEvent.Invoke(new SqlSugarClient(PrimaryConfig), new SqlSugarClient(SecondaryConfig));
                    SetIsNeedSyncData(!isSuccess);

                    if (isSuccess)
                    {
                        return DBSyncStart.SyncSuccess;
                    }
                    else
                    {
                        return DBSyncStart.SyncFailure;
                    }
                }
                catch (Exception ex)
                {
                    // 记录异常日志
                    Console.WriteLine($"同步数据时发生异常: {ex.Message}");
                    return DBSyncStart.SyncFailure;
                }
                finally
                {
                    SyncEvent.Set(); // 释放同步事件，以防止其他线程一直被阻塞
                }
            }

        }


        public static string GetDescription(DBSyncStart value)
        {

            FieldInfo field = value.GetType().GetField(value.ToString());



            DescriptionAttribute attribute = (DescriptionAttribute)field.GetCustomAttribute(typeof(DescriptionAttribute));


            return attribute == null ? value.ToString() : attribute.Description;
        }

        /// <summary>
        /// 检测目标地址是否打通
        /// </summary>
        /// <param name="ip">ip地址</param>
        /// <param name="port">端口号</param>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public static bool IsPortOpen(string ip, int port, int timeout = 300)
        {
            using (var client = new TcpClient())
            {
                var result = client.ConnectAsync(ip, port);
                try
                {
                    var open = result.Wait(timeout);
                    return open;
                }
                catch (SocketException)
                {
                    return false;
                }
            }
        }
        /// <summary>
        /// 检测目标地址是否打通：主数据库IP和端口是否打通（true通，false断)
        /// </summary>
        /// <param name="timeout">超时时间</param>
        /// <returns></returns>
        public static bool IsPortOpen(int timeout = 300)
        {
            string ip = Host;
            int port = Port;
            using (var client = new TcpClient())
            {
                bool isOpen = true;
                try
                {
                    var result = client.ConnectAsync(ip, port);
                    isOpen = result.Wait(timeout);
                    if (!isOpen)
                    {
                        //Console.WriteLine($"连接超时{ip},{port}");
                    }
                    return isOpen;
                }
                catch
                {
                    isOpen = false;
                    return isOpen;
                }
                finally
                {
                    //Console.WriteLine("网络检测:" + isOpen);
                }
            }
        }

        /// <summary>
        /// 返回数据库连接串
        /// </summary>
        /// <param name="dbType">数据库类型</param>
        /// <param name="host">服务器IP地址</param>
        /// <param name="dbName">数据库名</param>
        /// <param name="name">登录账户</param>
        /// <param name="password">登录密码</param>
        private static ConnectionConfig GetConnectionConfig(DbType dbType, string host, string port, string dbName, string name, string password)
        {
            ConnectionConfig config;
            string ConnectionString;
            switch (dbType)
            {
                case DbType.MySql:
                    ConnectionString = $"Server={host};DataBase={dbName};Port={port};UserId={name};Password={password};Persist Security Info=True;Allow Zero Datetime=True;Character Set=utf8;";
                    config = new ConnectionConfig()
                    {
                        ConnectionString = ConnectionString,//连接符字串
                        DbType = DbType.MySql,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute //从实体特性中读取主键自增列信息
                    };

                    break;
                case DbType.SqlServer:
                    ConnectionString = $"Server={host},{port};DataBase={dbName};uid={name};pwd={password}";
                    config = new ConnectionConfig()
                    {
                        ConnectionString = ConnectionString,//连接符字串
                        DbType = DbType.SqlServer,
                        IsAutoCloseConnection = true,
                        InitKeyType = InitKeyType.Attribute //从实体特性中读取主键自增列信息
                    };
                    break;
                default:

                    config = null;

                    break;
            }

            return config;


        }
    }

}
