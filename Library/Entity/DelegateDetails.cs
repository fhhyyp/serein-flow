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
        public DelegateDetails(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            this._emitMethodType = EmitMethodType;
            this._emitDelegate = EmitDelegate;
        }
        public void Upload(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            _emitMethodType = EmitMethodType;
            _emitDelegate = EmitDelegate;
        }
        private Delegate _emitDelegate;
        private EmitMethodType _emitMethodType;
        public Delegate EmitDelegate { get => _emitDelegate; }
        public EmitMethodType EmitMethodType { get => _emitMethodType; }

        /// <summary>
        /// 异步等待Emit创建的委托。
        /// 需要注意的是，传入的实例必须包含创建委托的方法信息，传入的参数也必须符合方法入参信息。
        /// </summary>
        /// <param name="instance">实例</param>
        /// <param name="args">入参</param>
        /// <returns>返回值</returns>
        public async Task<object> Invoke(object instance, object[] args)
        {
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
