namespace ScriptLang.Runtime
{
    public interface ICallable
    {
        Task<Value> CallAsync(ScriptEngine engine, params List<Value> args);
    }
}
