using System.Security.Cryptography;
using System.Text;
using ScriptLang.Runtime;

namespace ScriptLang.System
{
    internal static class Extension
    {
        extension<T1, T2>(T1)
        {
            /// <summary>模拟管道运算符</summary>
            /// <param name="x">输入参数</param>
            /// <param name="f">目标函数</param>
            /// <returns>目标函数返回值</returns>
            public static T2 operator >>(T1 x, Func<T1, T2> f) => f(x);
        }
    }

    [PrototypeExtension(NamingFormat = NamingFormat.Js)]
    internal sealed partial class CryptoModule : ScriptRuntimeObject<CryptoModule>
    {
        public partial bool IsTarget(Value value) => value is ClrObjectValue clr && clr.Value is CryptoModule;

        [PrototypeFunction] 
        [LspDoc("计算指定算法的哈希值（支持 md5/sha1/sha256/sha512）")]
        public static StringValue Hash(StringValue algorithm, StringValue data)
        {
            using HashAlgorithm hash = algorithm.Value.ToLower() switch
            {
                "md5" => MD5.Create(),
                "sha1" => SHA1.Create(),
                "sha256" => SHA256.Create(),
                "sha512" => SHA512.Create(),
                _ => throw new ArgumentException($"不支持的哈希算法: {algorithm.Value}")
            };

            // 管道运算符
            var hashValue = data.Value >> Encoding.UTF8.GetBytes
                                       >> hash.ComputeHash
                                       >> Convert.ToHexString 
                                       >> (x => x.ToLower())
                                       >> StringValue.Create;
            return hashValue;

        }

        [PrototypeFunction]
        [LspDoc("计算 HMAC 消息认证码\r\n支持 md5/sha1/sha256/sha512")]
        public static StringValue Hmac(StringValue algorithm, StringValue data, StringValue key)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key.Value);
            using HMAC hmac = algorithm.Value.ToLower() switch
            {
                "md5" => new HMACMD5(keyBytes),
                "sha1" => new HMACSHA1(keyBytes),
                "sha256" => new HMACSHA256(keyBytes),
                "sha512" => new HMACSHA512(keyBytes),
                _ => throw new ArgumentException($"不支持的 HMAC 算法: {algorithm.Value}")
            };
            var hmacValue = data.Value >> Encoding.UTF8.GetBytes
                                       >> hmac.ComputeHash
                                       >> Convert.ToHexString
                                       >> (x => x.ToLower())
                                       >> StringValue.Create;
            return hmacValue;
        }

        [PrototypeFunction]
        [LspDoc("生成指定长度的密码学安全随机字节（Base64 编码）")]
        public static StringValue RandomBytes(NumberValue<int> length) => StringValue.Create(Convert.ToBase64String(RandomNumberGenerator.GetBytes(length.Value)));

        [PrototypeFunction]
        [LspDoc("生成指定长度的随机字符串")]
        public static StringValue RandomString(NumberValue<int> length)
        { 
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890"; 
            var bytes = RandomNumberGenerator.GetBytes(length.Value); 
            var result = new char[length.Value];
            for (int i = 0; i < length.Value; i++)
                result[i] = chars[bytes[i] % chars.Length]; 
            return StringValue.Create(new string(result));
        }

        [PrototypeFunction] 
        [LspDoc("生成 UUID v4 标识符")]
        public static StringValue Uuid() => StringValue.Create(Guid.NewGuid().ToString());

        [PrototypeProperty] 
        [LspDoc("支持的哈希算法名称列表（md5/sha1/sha256/sha512）")]
        private static ObjectValue Algorithms() => new(new() 
        { 
            ["md5"] = StringValue.Create("MD5"), 
            ["sha1"] = StringValue.Create("SHA1"),
            ["sha256"] = StringValue.Create("SHA256"),
            ["sha512"] = StringValue.Create("SHA512") 
        });
    }
}
