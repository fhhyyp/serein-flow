using DynamicDemo.Node;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Serein.DynamicFlow
{

    public enum FfState
    {
        Succeed,
        Cancel,
    }
    /// <summary>
    /// 触发器上下文
    /// </summary>
    public class FlipflopContext
    {
        public FfState State { get; set; } 
        public  object? Data { get; set; }
        /*public FlipflopContext()
        {
            State = FfState.Cancel;
        }*/
        public FlipflopContext(FfState ffState,object? data = null)
        {
            State = ffState;
            Data = data;
        }
    }


    /// <summary>
    /// 动态流程上下文
    /// </summary>

    public class DynamicContext(IServiceContainer serviceContainer)

    {

        private readonly string contextGuid = "";//System.Guid.NewGuid().ToString();
        
        public IServiceContainer ServiceContainer { get; } = serviceContainer;
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
            NodeRunCts ??= ServiceContainer.Get<NodeRunTcs>();
            return Task.Factory.StartNew(async () =>
            {
                for(int i = 0; i < count; i++)
                {
                    NodeRunCts.Token.ThrowIfCancellationRequested();
                    await time;
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
