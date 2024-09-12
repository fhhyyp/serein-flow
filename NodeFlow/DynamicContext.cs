
using Serein.NodeFlow.Model;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Serein.NodeFlow
{

    //public enum FfState
    //{
    //    Succeed,
    //    Cancel,
    //    Error,
    //}

    //public class FlipflopContext
    //{
    //    public FlowStateType State { get; set; }
    //    public object? Data { get; set; }
    //    public FlipflopContext(FlowStateType ffState, object? data = null)
    //    {
    //        State = ffState;
    //        Data = data;
    //    }
    //}

    public static class FlipflopFunc
    {
        /// <summary>
        /// 传入触发器方法的返回类型，尝试获取Task[Flipflop[]] 中的泛型类型
        /// </summary>
        //public static Type GetFlipflopInnerType(Type type)
        //{
        //    // 检查是否为泛型类型且为 Task<>
        //    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        //    {
        //        // 获取 Task<> 的泛型参数类型，即 Flipflop<>
        //        var innerType = type.GetGenericArguments()[0];

        //        // 检查泛型参数是否为 Flipflop<>
        //        if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(FlipflopContext<>))
        //        {
        //            // 获取 Flipflop<> 的泛型参数类型，即 T
        //            var flipflopInnerType = innerType.GetGenericArguments()[0];

        //            // 返回 Flipflop<> 中的具体类型
        //            return flipflopInnerType;
        //        }
        //    }
        //    // 如果不符合条件，返回 null
        //    return null;
        //}

        public static bool IsTaskOfFlipflop(Type type)
        {
            // 检查是否为泛型类型且为 Task<>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
            {
                // 获取 Task<> 的泛型参数类型
                var innerType = type.GetGenericArguments()[0];

                // 检查泛型参数是否为 Flipflop<>
                if (innerType == typeof(FlipflopContext))
                //if (innerType.IsGenericType && innerType.GetGenericTypeDefinition() == typeof(FlipflopContext<>))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 触发器上下文
    /// </summary>
    public class FlipflopContext//<TResult> 
    {
        public LibraryCore.NodeFlow.FlowStateType State { get; set; }
        //public TResult? Data { get; set; }
        public object? Data { get; set; }
        public FlipflopContext(FlowStateType ffState)
        {
            State = ffState;
        }
        public FlipflopContext(FlowStateType ffState, object data)
        {
            State = ffState;
            Data = data;
        }


    }

    /// <summary>
    /// 动态流程上下文
    /// </summary>

    public class DynamicContext(ISereinIoc serviceContainer)

    {

        private readonly string contextGuid = "";//System.Guid.NewGuid().ToString();

        public ISereinIoc ServiceContainer { get; } = serviceContainer;
        private List<Type> InitServices { get; set; } = [];

        // private ConcurrentDictionary<string, object?> ContextData { get; set; } = [];

        //public void SetFlowData(object data)
        //{
        //    var threadId = Thread.CurrentThread.ManagedThreadId.ToString();
        //    var name = $"{threadId}.{contextGuid}FlowData";
        //    SetData(name,data);
        //}
        //public object GetFlowData(bool IsRetain = false)
        //{
        //    var threadId = Thread.CurrentThread.ManagedThreadId.ToString();
        //    var name = $"{threadId}.{contextGuid}FlowData";
        //    if (IsRetain)
        //    {
        //        return GetData(name);
        //    }
        //    else
        //    {
        //        return  GetAndRemoteData(name);

        //    }
        //}


        public void InitService<T>()
        {
            InitService(typeof(T));
        }
        public void InitService(Type type)
        {
            if (!InitServices.Contains(type))
            {
                InitServices.Add(type);
            }
            else
            {
                //throw new Exception("初始化时试图添加已存在的类型："+type.Name);
                Console.WriteLine("初始化时试图添加已存在的类型：" + type.Name);
            }
        }
        public void Biuld()
        {
            foreach (var item in InitServices)
            {
                ServiceContainer.Register(item);
            }
            ServiceContainer.Build();
        }

        //public object? RemoveData(string key)
        //{
        //    if (ContextData.Remove(key, out var data))
        //    {
        //        return data;
        //    }
        //    return null;
        //}

        //public void SetData<T>(string key, T value)
        //{
        //    ContextData[key] = value;
        //}

        //public T? GetData<T>(string key)
        //{
        //    if (ContextData.TryGetValue(key, out object? value))
        //    {
        //        if(value == null)
        //        {
        //            return default;
        //        }
        //        if (value.GetType() == typeof(T))
        //        {
        //            return (T)value;
        //        }

        //    }
        //    return default;
        //}

        //public object? GetData(string key)
        //{
        //    if (ContextData.TryGetValue(key, out object? value))
        //    {
        //        return value;
        //    }
        //    return null;
        //}


        //public ConcurrentDictionary<string,Task> FlipFlopTasks { get; set; } = [];

        public NodeRunTcs NodeRunCts { get; set; }
        public Task CreateTimingTask(Action action, int time = 100, int count = -1)
        {
            NodeRunCts ??= ServiceContainer.GetOrInstantiate<NodeRunTcs>();
            return Task.Factory.StartNew(async () =>
            {
                for (int i = 0; i < count; i++)
                {
                    NodeRunCts.Token.ThrowIfCancellationRequested();
                    await Task.Delay(time);
                    action.Invoke();
                }
            });
        }
    }

    public static class MyExtensions
    {
        public static TaskAwaiter GetAwaiter(this int i) => Task.Delay(i).GetAwaiter();
    }


    // if (time <= 0) throw new ArgumentException("时间不能≤0");
}
