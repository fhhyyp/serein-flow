using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Serein.Library.Utils.EmitHelper;

namespace Serein.Library
{
    /// <summary>
    /// Emit创建的委托描述，用于WebApi、WebSocket、NodeFlow动态调用方法的场景。
    /// 一般情况下你无须内部细节，只需要调用 Invoke() 方法即可。
    /// </summary>
    public class DelegateDetails
    {
        /// <summary>
        /// 根据方法信息构建Emit委托
        /// </summary>
        /// <param name="methodInfo"></param>
        public DelegateDetails(MethodInfo methodInfo) 
        {
            var emitMethodType = EmitHelper.CreateDynamicMethod(methodInfo, out var emitDelegate);
            _emitMethodType = emitMethodType;
            _emitDelegate = emitDelegate;
        }

        /// <summary>
        /// 记录Emit委托
        /// </summary>
        /// <param name="EmitMethodType"></param>
        /// <param name="EmitDelegate"></param>
        public DelegateDetails(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            _emitMethodType = EmitMethodType;
            _emitDelegate = EmitDelegate;
        }
        /// <summary>
        /// 更新委托方法
        /// </summary>
        /// <param name="EmitMethodType"></param>
        /// <param name="EmitDelegate"></param>
        public void Upload(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            _emitMethodType = EmitMethodType;
            _emitDelegate = EmitDelegate;
        }
        private Delegate _emitDelegate;
        private EmitMethodType _emitMethodType;

        ///// <summary>
        ///// <para>普通方法：Func&lt;object,object[],object&gt;</para>
        ///// <para>异步方法：Func&lt;object,object[],Task&gt;</para>
        ///// <para>异步有返回值方法：Func&lt;object,object[],Task&lt;object&gt;&gt;</para>
        ///// </summary>
        //public Delegate EmitDelegate { get => _emitDelegate; }
        ///// <summary>
        ///// 表示Emit构造的委托类型
        ///// </summary>
        //public EmitMethodType EmitMethodType { get => _emitMethodType; }

        /// <summary>
        /// <para>使用的实例必须能够正确调用该委托，传入的参数也必须符合方法入参信息。</para>
        ///  </summary>
        /// <param name="instance">拥有符合委托签名的方法信息的实例</param>
        /// <param name="args">如果方法没有入参，也需要传入一个空数组</param>
        /// <returns>void方法自动返回null</returns>
        public async Task<object> InvokeAsync(object instance, object[] args)
        {
            if (args is null)
            {
                args = Array.Empty<object>();
            }
            object result = null;
            try
            {
                if (_emitMethodType == EmitMethodType.HasResultTask && _emitDelegate is Func<object, object[], Task<object>> hasResultTask)
                {
                    result = await hasResultTask(instance, args);
                }
                else if (_emitMethodType == EmitMethodType.Task && _emitDelegate is Func<object, object[], Task> task)
                {
                    await task.Invoke(instance, args);
                }
                else if (_emitMethodType == EmitMethodType.Func && _emitDelegate is Func<object, object[], object> func)
                {
                    result = func.Invoke(instance, args);
                }
                else
                {
                    throw new NotImplementedException("创建了非预期委托（应该不会出现）");
                }
                return result;
            }
            catch
            {
                throw;
            }
        }
    }
}
