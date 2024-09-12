using Serein.Library.Api;
using Serein.Library.Attributes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Serein.Library.Framework.IOC
{

    public class SereinIoc : ISereinIoc
    {

        private readonly ConcurrentDictionary<string, object> _dependencies;
        private readonly ConcurrentDictionary<string, Type> _typeMappings;
        private readonly List<Type> _waitingForInstantiation;

        public SereinIoc()
        {

            _dependencies = new ConcurrentDictionary<string, object>
            {
                [typeof(ISereinIoc).FullName] = this
            };

            _typeMappings = new ConcurrentDictionary<string, Type>();
            _waitingForInstantiation = new List<Type>();
        }
        public object GetOrCreateServiceInstance(Type type, params object[] parameters)
        {
            Register(type);
            object instance;

            if (_dependencies.ContainsKey(type.FullName))
            {
                instance = _dependencies[type.FullName];
            }
            else
            {

                instance = Activator.CreateInstance(type);


                _dependencies[type.FullName] = instance;

            }


            return instance;

        }
        public T CreateServiceInstance<T>(params object[] parameters)
        {
            return (T)GetOrCreateServiceInstance(typeof(T), parameters);
        }

        public ISereinIoc Reset()
        {
            foreach(var instancei in _dependencies.Values)
            {
                if (typeof(IDisposable).IsAssignableFrom(instancei.GetType()) && instancei is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            _dependencies.Clear();
            _waitingForInstantiation.Clear();
            //_typeMappings.Clear();
            return this;
        }

        public ISereinIoc Register(Type type, params object[] parameters)
        {

            if (!_typeMappings.ContainsKey(type.FullName))
            {
                _typeMappings[type.FullName] = type;
            }

            return this;
        }
        public ISereinIoc Register<T>(params object[] parameters)
        {
            Register(typeof(T), parameters);
            return this;
        }

        public ISereinIoc Register<TService, TImplementation>(params object[] parameters)
            where TImplementation : TService
        {
            _typeMappings[typeof(TService).FullName] = typeof(TImplementation);
            return this;
        }

        public object GetOrInstantiate(Type type)
        {


            if (!_dependencies.TryGetValue(type.FullName, out object value))
            {
                Register(type);

                value = Instantiate(type);

                InjectDependencies(type);
            }



            return value;

        }


        public T GetOrInstantiate<T>()
        {
            if(!_dependencies.TryGetValue(typeof(T).FullName, out object value))
            {
                Register<T>();

                value = Instantiate(typeof(T));
            }

            return (T)value;
            //throw new InvalidOperationException("目标类型未创建实例");
        }
        public ISereinIoc Build()
        {
            foreach (var type in _typeMappings.Values)
            {

                if(!_dependencies.ContainsKey(type.FullName))
                {

                    _dependencies[type.FullName] = Activator.CreateInstance(type);

                }

            }

            foreach (var instance in _dependencies.Values)
            {

                InjectDependencies(instance); // 替换占位符
            }

            //var instance = Instantiate(item.Value);

            TryInstantiateWaitingDependencies();
            return this;
        }

        public object Instantiate(Type controllerType, params object[] parameters)
        {
            var instance = Activator.CreateInstance(controllerType, parameters);
            if(instance != null)
            {
                InjectDependencies(instance);
            }
            return instance;
        }

        private void InjectDependencies(object instance)
        {
            var properties = instance.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).ToArray()
                                              .Where(p => p.CanWrite && p.GetCustomAttribute<AutoInjectionAttribute>() != null);

            foreach (var property in properties)
            {
                var propertyType = property.PropertyType;

                if (_dependencies.TryGetValue(propertyType.FullName, out var dependencyInstance))
                {
                    property.SetValue(instance, dependencyInstance);
                }

            }
        }

        private void TryInstantiateWaitingDependencies()
        {
            foreach (var waitingType in _waitingForInstantiation.ToList())
            {
                if (_typeMappings.TryGetValue(waitingType.FullName, out var implementationType))
                {
                    var instance = Instantiate(implementationType);
                    if (instance != null)
                    {

                        _dependencies[waitingType.FullName] = instance;

                        _waitingForInstantiation.Remove(waitingType);
                    }
                }
            }
        }

        #region run()
        public ISereinIoc Run<T>(Action<T> action)
        {
            var service = GetOrInstantiate<T>();
            if (service != null)
            {
                action(service);
            }
            return this;
        }

        public ISereinIoc Run<T1, T2>(Action<T1, T2> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();

            action(service1, service2);
            return this;
        }

        public ISereinIoc Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();
            var service3 = GetOrInstantiate<T3>();
            action(service1, service2, service3);
            return this;
        }

        public ISereinIoc Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();
            var service3 = GetOrInstantiate<T3>();
            var service4 = GetOrInstantiate<T4>();
            action(service1, service2, service3, service4);
            return this;
        }

        public ISereinIoc Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();
            var service3 = GetOrInstantiate<T3>();
            var service4 = GetOrInstantiate<T4>();
            var service5 = GetOrInstantiate<T5>();
            action(service1, service2, service3, service4, service5);
            return this;
        }

        public ISereinIoc Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();
            var service3 = GetOrInstantiate<T3>();
            var service4 = GetOrInstantiate<T4>();
            var service5 = GetOrInstantiate<T5>();
            var service6 = GetOrInstantiate<T6>();
            action(service1, service2, service3, service4, service5, service6);
            return this;
        }

        public ISereinIoc Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();
            var service3 = GetOrInstantiate<T3>();
            var service4 = GetOrInstantiate<T4>();
            var service5 = GetOrInstantiate<T5>();
            var service6 = GetOrInstantiate<T6>();
            var service7 = GetOrInstantiate<T7>();
            action(service1, service2, service3, service4, service5, service6, service7);
            return this;
        }

        public ISereinIoc Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            var service1 = GetOrInstantiate<T1>();
            var service2 = GetOrInstantiate<T2>();
            var service3 = GetOrInstantiate<T3>();
            var service4 = GetOrInstantiate<T4>();
            var service5 = GetOrInstantiate<T5>();
            var service6 = GetOrInstantiate<T6>();
            var service7 = GetOrInstantiate<T7>();
            var service8 = GetOrInstantiate<T8>();
            action(service1, service2, service3, service4, service5, service6, service7, service8);
            return this;
        }

        #endregion
    }


    /* public interface IServiceContainer
     {
         ServiceContainer Register<T>(params object[] parameters);
         ServiceContainer Register<TService, TImplementation>(params object[] parameters) where TImplementation : TService;
         TService Resolve<TService>();
         void Get<T>(Action<T> action);
         object Instantiate(Type type, params object[] parameters);

     }
     public class ServiceContainer : IServiceContainer
     {
         private readonly Dictionary<Type, object> _dependencies;
         public ServiceContainer()
         {
             _dependencies = new Dictionary<Type, object>
             {
                 [typeof(IServiceContainer)] = this
             };
         }

         public void Get<T>(Action<T> action)
         {
             var service = Resolve<T>();
             action(service);
         }
         public ServiceContainer Register<T>(params object[] parameters)
         {
             var instance = Instantiate(typeof(T), parameters);
             _dependencies[typeof(T)] = instance;
             return this;
         }

         public ServiceContainer Register<TService, TImplementation>(params object[] parameters)
             where TImplementation : TService
         {

             _dependencies[typeof(TService)] = Instantiate(typeof(TImplementation), parameters);
             return this;
         }


         public TService Resolve<TService>()
         {
             return (TService)_dependencies[typeof(TService)];
         }

         public object Instantiate(Type controllerType, params object[] parameters)
         {
             var constructors = controllerType.GetConstructors(); // 获取控制器的所有构造函数

             // 查找具有最多参数的构造函数
             var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).FirstOrDefault();

             if (constructor != null)
             {
                 if (parameters.Length > 0)
                 {
                     return Activator.CreateInstance(controllerType, parameters);
                 }
                 else {
                     var tmpParameters = constructor.GetParameters();
                     var dependencyInstances = new List<object>();

                     foreach (var parameter in tmpParameters)
                     {
                         var parameterType = parameter.ParameterType;
                         _dependencies.TryGetValue(parameterType, out var dependencyInstance);
                         dependencyInstances.Add(dependencyInstance);
                         if (dependencyInstance == null)
                         {
                             return null;
                         }
                     }
                     // 用解析的依赖项实例化目标类型
                     return Activator.CreateInstance(controllerType, dependencyInstances.ToArray());
                 }
             }
             else
             {
                 return Activator.CreateInstance(controllerType);
             }
         }
     }*/



}
