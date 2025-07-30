namespace Serein.Library.Api
{
    /// <summary>
    /// 枚举转换器接口
    /// </summary>
    /// <typeparam name="TEnum"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public interface IEnumConvertor<TEnum, TValue>
    {
        /// <summary>
        /// 将枚举值转换为指定类型的值
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        TValue Convertor(TEnum e);
    }

}
