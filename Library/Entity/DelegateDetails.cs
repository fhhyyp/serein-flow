using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Serein.Library.Utils.EmitHelper;

namespace Serein.Library.Entity
{
    /// <summary>
    /// Emit创建的委托描述，用于WebApi、WebSocket、NodeFlow动态调用方法的场景。
    /// 一般情况下你无须内部细节，只需要调用 Invoke() 方法即可。
    /// </summary>
    public class DelegateDetails
    {
        /// <summary>
        /// 记录Emit委托
        /// </summary>
        /// <param name="EmitMethodType"></param>
        /// <param name="EmitDelegate"></param>
        public DelegateDetails(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            this._emitMethodType = EmitMethodType;
            this._emitDelegate = EmitDelegate;
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

        /// <summary>
        /// <para>普通方法：Func&lt;object,object[],object&gt;</para>
        /// <para>异步方法：Func&lt;object,object[],Task&gt;</para>
        /// <para>异步有返回值方法：Func&lt;object,object[],Task&lt;object&gt;&gt;</para>
        /// </summary>
        public Delegate EmitDelegate { get => _emitDelegate; }
        /// <summary>
        /// 表示Emit构造的委托类型
        /// </summary>
        public EmitMethodType EmitMethodType { get => _emitMethodType; }

        /// <summary>
        /// <para>使用的实例必须能够正确调用该委托，传入的参数也必须符合方法入参信息。</para>
        ///  </summary>
        /// <param name="instance">实例</param>
        /// <param name="args">入参</param>
        /// <returns>void方法自动返回null</returns>
        public async Task<object> InvokeAsync(object instance, object[] args)
        {
            if(args is null)
            {
                args = new object[0];
            }
            object result = null;
            try
            {
                if (EmitMethodType == EmitMethodType.HasResultTask && EmitDelegate is Func<object, object[], Task<object>> hasResultTask)
                {
                    result = await hasResultTask(instance, args);
                }
                else if (EmitMethodType == EmitMethodType.Task && EmitDelegate is Func<object, object[], Task> task)
                {
                    await task.Invoke(instance, args);
                    result = null;
                }
                else if (EmitMethodType == EmitMethodType.Func && EmitDelegate is Func<object, object[], object> func)
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
