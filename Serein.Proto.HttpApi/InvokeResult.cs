namespace Serein.Proto.HttpApi
{
    /// <summary>
    /// 调用结果
    /// </summary>
    public class InvokeResult
    {
        /// <summary>
        /// 处理工具记录的请求Id，用于匹配请求与响应
        /// </summary>
        public long RequestId { get; set; } 
        /// <summary>
        /// 调用状态
        /// </summary>
        public HandleState State { get; set; }
        /// <summary>
        /// 调用正常时这里会有数据
        /// </summary>
        public object? Data{ get; set; }
        /// <summary>
        /// 调用失败时这里可能会有异常数据
        /// </summary>
        public Exception? Exception { get; set; }

        /// <summary>
        /// 调用成功
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public static InvokeResult Ok(long requestId, object? data) => new InvokeResult
        {
            RequestId = requestId,
            Data = data,
            State = HandleState.Ok,
        };

        /// <summary>
        /// 调用失败
        /// </summary>
        /// <param name="requestId"></param>
        /// <param name="state"></param>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static InvokeResult Fail(long requestId, HandleState state, Exception? ex = null) => new InvokeResult
        {
            RequestId = requestId,
            State = state,
            Exception = ex,
        };
    }


    
}

