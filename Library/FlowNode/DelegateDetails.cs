using Serein.Library.Utils;
using System;
using System.Reflection;
using System.Threading.Tasks;
using static Serein.Library.Utils.EmitHelper;

namespace Serein.Library
{
    /// <summary>
    /// 通过 Emit 创建委托，代替反射调用方法，实现高性能的动态调用。
    /// 一般情况下你无须内部细节，只需要调用 Invoke() 方法即可。
    /// </summary>
    public class DelegateDetails
    {
        private readonly EmitType emitType = EmitType.None;

        /// <summary>
        /// 创建的委托类型
        /// </summary>
        public enum EmitType 
        { 
            /// <summary>
            /// 默认
            /// </summary>
            None,
            /// <summary>
            /// 方法调用
            /// </summary>
            MethodInvoke,
            /// <summary>
            /// 字段赋值
            /// </summary>
            FieldSetter,
            /// <summary>
            /// 字段取值
            /// </summary>
            FieldGetter,
            /// <summary>
            /// 属性赋值
            /// </summary>
            PropertySetter,
            /// <summary>
            /// 属性取值
            /// </summary>
            PropertyGetter,
            /// <summary>
            /// 集合取值
            /// </summary>
            CollectionGetter,
            /// <summary>
            /// 集合赋值
            /// </summary>
            CollectionSetter,
            /// <summary>
            /// 数组创建
            /// </summary>
            ArrayCreate,
        }

        /// <summary>
        /// 表示方法的类型
        /// </summary>
        public enum GSType 
        {
            /// <summary>
            /// 获取值
            /// </summary>
            Get,
            /// <summary>
            /// 设置值
            /// </summary>
            Set,
        }


        /// <summary>
        /// 根据方法信息构建Emit委托
        /// </summary>
        /// <param name="methodInfo"></param>
        public DelegateDetails(MethodInfo methodInfo) 
        {
            emitType = EmitType.MethodInvoke;
            var emitMethodType = EmitHelper.CreateMethod(methodInfo, out var emitDelegate);
            _emitMethodInfo = emitMethodType;
            _emitDelegate = emitDelegate;
            methodType = _emitMethodInfo.EmitMethodType;
            if (_emitDelegate is Func<object, object[], Task<object>> hasResultTask)
            {
                this.methodHasResultTask = hasResultTask;
            }
            else if (_emitDelegate is Func<object, object[], Task> task)
            {
                this.methodTask = task;
            }
            else if (_emitDelegate is Func<object, object[], object> func)
            {
                this.methodInvoke = func;
            }
            else
            {
                throw new NotSupportedException();
            }
        }



        /// <summary>
        /// 根据字段信息构建Emit取/赋值委托
        /// </summary>
        /// <param name="fieldInfo">字段信息</param>
        /// <param name="gsType">是否为 get，如果不是，则为 set</param>
        public DelegateDetails(FieldInfo fieldInfo, GSType gsType) 
        {
            if (gsType == GSType.Get)
            {
                emitType = EmitType.FieldGetter;
                getter = EmitHelper.CreateFieldGetter(fieldInfo);

            }
            else if (gsType == GSType.Set)
            {
                emitType = EmitType.FieldSetter;
                setter = EmitHelper.CreateFieldSetter(fieldInfo);
            }
            else
            {
                throw new NotSupportedException("错误的构建类型");
            }

        }

        /// <summary>
        /// 根据字段信息构建Emit取/赋值委托
        /// </summary>
        /// <param name="propertyInfo">字段信息</param>
        /// <param name="gsType">是否为 get，如果不是，则为 set</param>
        public DelegateDetails(PropertyInfo propertyInfo, GSType gsType) 
        {
            if (gsType == GSType.Get)
            {
                emitType = EmitType.PropertyGetter;
                getter = EmitHelper.CreatePropertyGetter(propertyInfo);

            }
            else if (gsType == GSType.Set)
            {
                emitType = EmitType.PropertySetter;
                setter = EmitHelper.CreatePropertySetter(propertyInfo);
            }
            else
            {
                throw new NotSupportedException("错误的构建类型");
            }

        }

        /// <summary>
        /// 目前提供了创建集合取值/赋值委托
        /// </summary>
        /// <param name="type">类型信息</param>
        /// <param name="emitType">操作类型</param>
        public DelegateDetails(Type type, EmitType emitType, Type? itemType = null) 
        {
            if (emitType == EmitType.CollectionSetter)
            {
                this.emitType = EmitType.CollectionSetter;
                collectionSetter = EmitHelper.CreateCollectionSetter(type);

            }
            else if (emitType == EmitType.CollectionGetter)
            {
                this.emitType = EmitType.CollectionGetter;
                collectionGetter = EmitHelper.CreateCollectionGetter(type, itemType);
            }
            else if (emitType == EmitType.ArrayCreate)
            {
                Func<int, object> func = EmitHelper.CreateArrayFactory(type);
                this.arrayCreatefunc = func;
                this.emitType = EmitType.ArrayCreate;
            }
            else
            {
                throw new NotSupportedException("错误的构建类型");
            }
            
        }

      



        private Func<object,object, object> collectionGetter= null;
        private Action<object,object, object> collectionSetter = null;


        private Func<object, object> getter = null;
        private Action<object, object> setter = null;


        private Func<object, object[], Task<object>> methodHasResultTask = null;
        private Func<object, object[], Task> methodTask = null;
        private Func<object, object[], object> methodInvoke = null;

        private Func<int, object> arrayCreatefunc = null;

        
        /*/// <summary>
        /// 更新委托方法
        /// </summary>
        /// <param name="EmitMethodType"></param>
        /// <param name="EmitDelegate"></param>
        public void Upload(EmitMethodType EmitMethodType, Delegate EmitDelegate)
        {
            _emitMethodType = EmitMethodType;
            _emitDelegate = EmitDelegate;
        }*/

        private Delegate _emitDelegate;
        private EmitMethodInfo _emitMethodInfo;


        private EmitMethodType methodType;

        /// <summary>
        /// 该Emit委托的相应信息
        /// </summary>
        public EmitMethodInfo EmitMethodInfo => _emitMethodInfo;

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
        /// <param name="instance">拥有符合委托签名的实例</param>
        /// <param name="args">如果不需要入参，也需要传入一个空数组,而不能为 null</param>
        /// <returns>void方法、setter自动返回null</returns>
        public async Task<object> InvokeAsync(object instance, object[] args)
        {
            if (emitType == EmitType.MethodInvoke)
            {
                return await MethodInvoke(instance, args);
            }
            else if (emitType == EmitType.PropertyGetter || emitType == EmitType.FieldGetter)
            {
                return getter(instance);
            }
            else if (emitType == EmitType.PropertySetter || emitType == EmitType.FieldSetter)
            {
                setter(instance, args[0]);
                return null;
            }
            else if (emitType == EmitType.CollectionGetter)
            {
                return collectionGetter(instance, args[0]);
            }
            else if (emitType == EmitType.CollectionSetter)
            {
                collectionSetter(instance, args[0], args[1]);
                return null;
            }
            else if(emitType == EmitType.ArrayCreate)
            {
                if(args[0] is int count)
                {
                    return arrayCreatefunc(count);
                }

                
            }
            throw new NotSupportedException("当前委托类型不支持 InvokeAsync 方法。请使用其他方法调用。");

        }

        private async Task<object> MethodInvoke(object instance, object[] args)
        {
            if (args is null)
            {
                args = Array.Empty<object>();
            }
            if (_emitMethodInfo.IsStatic)
            {
                instance = null;
            }
            object result = null;
            if (methodType == EmitMethodType.Func)
            {
                result = methodInvoke.Invoke(instance, args);

            }
            else if (methodType == EmitMethodType.TaskHasResult)
            {
                result = await methodHasResultTask(instance, args);
            }
            else if (methodType == EmitMethodType.Task)
            {
                await methodTask(instance, args);
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
