namespace ScriptLang.System
{
    internal class ScriptRuntimeObject<T> where T : new()
    {
        private static readonly Lazy<T> _lazy = new Lazy<T>(() => new T());
        public static T Instance => _lazy.Value;
    }

}


