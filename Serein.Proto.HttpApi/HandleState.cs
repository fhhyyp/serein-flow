namespace Serein.Proto.HttpApi
{
    public enum HandleState
    {
        /// <summary>
        /// 默认值
        /// </summary>
        None ,

        /// <summary>
        /// 没有异常
        /// </summary>
        Ok,

        /// <summary>
        /// 没有对应的控制器
        /// </summary>
        NotHanleController,

        /// <summary>
        /// 没有对应的Http请求类型
        /// </summary>
        NotHttpApiRequestType,

        /// <summary>
        /// 没有处理配置
        /// </summary>
        NotHandleConfig,

        /// <summary>
        /// 无法实例化控制器
        /// </summary>
        HanleControllerIsNull,

        /// <summary>
        /// 调用参数获取错误
        /// </summary>
        InvokeArgError,

        /// <summary>
        /// 调用发生异常
        /// </summary>
        InvokeErrored,

        /// <summary>
        /// 请求被阻止
        /// </summary>
        RequestBlocked,
    }


    
}

