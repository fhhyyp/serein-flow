using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using static ScriptLang.Utils.EmitHelper;

namespace ScriptLang.Utils
{
    /// <summary>
    /// 通过 Emit 创建委托，代替反射调用方法，实现高性能的动态调用。
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
            _emitMethodType = EmitHelper.CreateMethod(methodInfo, out var emitDelegate);
            _emitDelegate = emitDelegate;
            if (_emitDelegate is Func<object, object[], Task<object>> hasResultTask)
            {
                this.methodHasResultTask = hasResultTask!;
            }
            else if (_emitDelegate is Func<object, object[], Task> task)
            {
                this.methodTask = task!;
            }
            else if (_emitDelegate is Func<object, object[], object> func)
            {
                this.methodInvoke = func!;
            }
            else
            {
                throw new NotSupportedException();
            }

            MethodInfo = methodInfo;
        }

        private readonly Func<object?, object?[]?, Task<object?>>? methodHasResultTask = null;
        private readonly Func<object?, object?[]?, Task>? methodTask = null;
        private readonly Func<object?, object?[]?, object?>? methodInvoke = null;
        public EmitMethodInfo _emitMethodType;
        private readonly Delegate _emitDelegate;

        public MethodInfo MethodInfo { get; }

        public async Task<object?> InvokeAsync(object? instance, object?[]? args = null)
        {
            args ??= [];
            if (_emitMethodType.IsStatic)
            {
                instance = null;
            }
            object? result = null;
            if (methodInvoke is not null)
            {
                result = methodInvoke?.Invoke(instance, args);
            }
            else if (methodHasResultTask is not null)
            {
                result = await methodHasResultTask.Invoke(instance, args);
            }
            else if (methodTask is not null)
            {
                await methodTask.Invoke(instance, args);
                result = null;
            }
            else
            {
                throw new NotImplementedException("创建了非预期委托（应该不会出现）");
            }
            return result;
        }

    }
}
