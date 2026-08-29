using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class NetworkModule : ScriptRuntimeObject<NetworkModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is NetworkModule;

        [PrototypeFunction] 
        [LspDoc("发送 HTTP GET 请求\r\noptions.headers 可设置请求头")]
        public static async Task<ObjectValue> HttpGet(StringValue url, ObjectValue? options = null)
        { 
            using var client = new HttpClient(); 
            if (options is not null && options.TryGetValue("headers", out var h))
            { 
                foreach (var (k, v) in h.AsObject()) 
                    client.DefaultRequestHeaders.TryAddWithoutValidation(k, v.AsString());
            } 
            var resp = await client.GetAsync(url.Value); 
            var content = await resp.Content.ReadAsStringAsync();
            return new ObjectValue(new() 
            { 
                ["status"] = NumberValueFactory.Create((int)resp.StatusCode),
                ["statusText"] = StringValue.Create(resp.ReasonPhrase ?? ""), 
                ["headers"] = new ObjectValue(resp.Headers.ToDictionary(x => x.Key, x => (Value)StringValue.Create(string.Join(",", x.Value)))), 
                ["data"] = StringValue.Create(content) });
        }

        [PrototypeFunction] 
        [LspDoc("发送 HTTP POST 请求\r\ncontentType 默认为 application/json")]
        public static async Task<ObjectValue> HttpPost(StringValue url, Value data, StringValue? contentType = null)
        {
            using var client = new HttpClient();
            HttpContent? content = null; 
            if (data != null) { 
                var json = data.IsString ? data.AsString() : JsonSerializer.Serialize(data); 
                content = new StringContent(json, Encoding.UTF8, contentType?.Value ?? "application/json"); 
            } 
            var resp = await client.PostAsync(url.Value, content); 
            var respContent = await resp.Content.ReadAsStringAsync(); 
            return new ObjectValue(new() 
            { 
                ["status"] = NumberValueFactory.Create((int)resp.StatusCode),
                ["statusText"] = StringValue.Create(resp.ReasonPhrase ?? ""), 
                ["data"] = StringValue.Create(respContent)
            }); 
        }
    }
}
