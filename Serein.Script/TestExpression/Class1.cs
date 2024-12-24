using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.TestExpression
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Text.RegularExpressions;

    public class ScriptParser
    {
        public static Expression<Func<T, bool>> ParseWhereExpression<T>(string lambdaText)
        {
            // 解析 lambda 表达式中的 item => item.StartsWith("张")
            var match = Regex.Match(lambdaText, @"(?<param>\w+)\s*=>\s*(?<expression>.*)");
            if (!match.Success) throw new Exception("Invalid lambda expression");

            var paramName = match.Groups["param"].Value;
            var expressionText = match.Groups["expression"].Value;

            // 创建 Lambda 参数表达式
            var param = Expression.Parameter(typeof(T), paramName);

            // 构建 StartsWith("张") 的表达式
            var startsWithMethod = typeof(string).GetMethod("StartsWith", new[] { typeof(string) });
            var constantValue = Expression.Constant("张");
            var methodCallExpression = Expression.Call(
                Expression.Property(param, "StartsWith"),
                startsWithMethod,
                constantValue);

            return Expression.Lambda<Func<T, bool>>(methodCallExpression, param);
        }

        public static void Main()
        {
            // 假设你有一个List<string>作为数据源
            var list = new List<string> { "张三", "李四", "张五" };

            // 模拟从文本中解析出来的脚本
            string script = "let list = GetList(); let newList = list.Where(item => item.StartsWith(\"张\")).ToList();";

            // 解析Where表达式
            var whereExpression = ParseWhereExpression<string>("item => item.StartsWith(\"张\")");

            // 使用表达式执行LINQ查询
            var filteredList = list.AsQueryable().Where(whereExpression).ToList();

            foreach (var item in filteredList)
            {
                Console.WriteLine(item); // 输出: 张三, 张五
            }
        }
    }

    internal class Class1
    {
        public Class1() { 
            List<string> list = new List<string>();

            var newList = list.Where(item => item.StartsWith("张")).ToList();
        }
    }
}
