using Serein.Library.Utils;

namespace Serein.Extend.NewtonsoftJson
{
    public static class NewtonsoftJsonExtend
    {
        public static string ToJsonText(this object data)
        {
            return JsonHelper.Serialize(data);
        }
    }
}
