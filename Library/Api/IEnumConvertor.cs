namespace Serein.Library.Api
{
    /// <summary>
    /// 枚举转换器接口
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface IEnumConvertor<TEnum, TValue>
    {
        TValue Convertor(TEnum e);
    }

}
