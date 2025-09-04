using Serein.Library.Utils;

namespace Serein.Extend.NewtonsoftJson
{
    public static class NewtonsoftJsonExtend
    {
        /// <summary>
        /// 对象转 Json 文本
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static string ToJsonText(this object data)
        {
            return JsonHelper.Serialize(data);
        }

        /// <summary>
        /// Json 文本转对象
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="jsonText"></param>
        /// <returns></returns>
        public static T ToJsonObject<T>(this string jsonText) where T : class
        {
            return JsonHelper.Deserialize<T>(jsonText);
        }
    }
}
