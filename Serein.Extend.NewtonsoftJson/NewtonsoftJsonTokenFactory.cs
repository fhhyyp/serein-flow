using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Extend.NewtonsoftJson
{
    public static class NewtonsoftJsonTokenFactory
    {
        public static IJsonToken Parse(string json)
        {
            var jt = JToken.Parse(json);
            return FromJToken(jt);
        }

        public static IJsonToken FromJToken(JToken token)
        {
            return token.Type switch
            {
                JTokenType.Object => new NewtonsoftJsonObjectToken((JObject)token),
                JTokenType.Array => new NewtonsoftJsonArrayToken((JArray)token),
                _ => new NewtonsoftJsonValueToken(token)
            };
        }
    }

}
