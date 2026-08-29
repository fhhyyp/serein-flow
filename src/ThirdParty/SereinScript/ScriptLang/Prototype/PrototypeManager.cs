using ScriptLang.Runtime;
using System.Diagnostics.CodeAnalysis;

namespace ScriptLang
{
    public class PrototypeManager
    {
        public List<IPrototype> Prototypes { get; } = new List<IPrototype>();

        private readonly ScriptEngine _engine;

        public PrototypeManager(ScriptEngine engine) 
        {
            _engine = engine;
        }

        public void Register<T>() where T : IPrototype ,new ()
        {
            var prototype = new T();
            prototype.Init();
            Prototypes.Add(prototype);
        }

        public void Register<T>(T instance) where T : IPrototype 
        {
            instance.Init();
            Prototypes.Add(instance);
        }

        public bool TryGetValue(Value target, string menber, [NotNullWhen(true)]out Value? value)
        {
            foreach(IPrototype prototypes in Prototypes)
            {
                if (prototypes.IsTarget(target))
                {
                    var result = prototypes.GetMethod(target, menber, _engine);
                    if(result is not null)
                    {
                        value = result;
                        return true;
                    }
                }
            }
            value = null;
            return false;
        }
    }


}
