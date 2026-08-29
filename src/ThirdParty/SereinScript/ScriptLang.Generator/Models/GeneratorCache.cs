#nullable disable

using Microsoft.CodeAnalysis;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScriptLang.Generator.Models
{
    /// <summary>
    /// 代码生成容器
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class GeneratorCache<T> 
    {
        /// <summary>
        /// 源生成器的上下文对象，提供了与生成器运行环境交互的接口，例如报告诊断信息、访问编译器提供的服务等。
        /// </summary>
        public SourceProductionContext Context { get; }

        /// <summary>
        /// 生成任务目标的缓存对象，通常包含了生成过程中需要使用的各种信息和数据，例如类的语法节点、符号信息、属性列表等。
        /// 这些信息可以帮助生成器在构建代码时做出正确的决策，并且可以避免重复计算，提高生成效率。
        /// </summary>
        public T Cache { get;  }

        /// <summary>
        /// 构建代码的 StringBuilder 对象，用于在生成过程中逐步构建最终的代码字符串。
        /// </summary>
        private StringBuilder Builder { get; }

        /// <summary>
        /// 指示是否取消生成，如果在生成过程中检测到取消请求，可以通过这个属性来决定是否提前终止生成过程，以节省资源和提高性能。
        /// </summary>

        public bool IsCancel => Context.CancellationToken.IsCancellationRequested;

        
        public GeneratorCache(SourceProductionContext context, T cache, StringBuilder builder)
        {
            Context = context;
            Cache = cache;
            Builder = builder;
        }

        /// <summary>
        /// 缩进数量
        /// </summary>
        private int retractCount = 0;

        /// <summary>
        /// 增加缩进
        /// </summary>
        public void IncreaseTab() => retractCount++;

        /// <summary>
        /// 减少缩进
        /// </summary>
        public void DecreaseTab() => retractCount--;

        /// <summary>
        /// 增加一行代码
        /// </summary>
        /// <param name="code">需要插入的代码</param>
        public GeneratorCache<T> AppendCode(string code)
        {
            var retract = new string(' ', retractCount * 4);
            Builder.Append(retract);
            Builder.AppendLine(code);
            return this;
        }


        /// <summary>
        /// 遍历集合并插入对应的代码
        /// </summary>
        /// <param name="items"></param>
        /// <param name="execute"></param>
        public GeneratorCache<T> AppendCode<TItems>(IEnumerable<TItems> items, Action<TItems> execute)
        {
            foreach(var item in items)
            {
                execute(item);
            }
            return this;
        }


        /// <summary>
        /// 插入对应的代码块
        /// </summary>
        /// <param name="execute"></param>
        public GeneratorCache<T> AppendCodeBlok(Action execute)
        {
            AppendCode("{");
            IncreaseTab();
            execute();
            DecreaseTab();
            AppendCode("}");
            return this;
        }

        /// <summary>
        /// 插入对应的代码块
        /// </summary>
        /// <param name="execute"></param>
        public GeneratorCache<T> AppendTab(Action execute)
        {
            IncreaseTab();
            execute();
            DecreaseTab();
            return this;
        }


        /// <summary>
        /// 增加一行空行
        /// </summary>
        public GeneratorCache<T> AppendCode()
        {
            Builder.AppendLine(string.Empty);
            return this;
        }

        /// <summary>
        /// 生成代码
        /// </summary>
        /// <returns></returns>
        public string ToCode() { return Builder.ToString(); }


        /*public void Tab(Action action)
        {
            IncreaseTab();
            action.Invoke();
            DecreaseTab();
        }*/


        /// <summary>
        /// 插入继承文档注释
        /// </summary>
        /// <param name="target">目标命名空间/属性/字段/类型</param>
        public void Inheritdoc(string target)
        {
            AppendCode($"/// <inheritdoc cref=\"{target}\"/>"); // 继承文档
        }
        #region summarys 注释插入

        /// <summary>
        /// 插入 summarys 注释
        /// </summary>
        /// <param name="summarys">摘要信息</param>
        /// <param name="targetsRemarks">参数备注信息</param>
        /// <param name="returns">返回值描述</param>
        public void Summarys(string[] summarys, (string, string)[] targetsRemarks = null, string returns = null)
        {
            AppendCode($"/// <summary>");
            foreach (var summary in summarys)
            {
                AppendCode($"/// <para>{summary}</para>");
            }
            AppendCode($"/// </summary>");
            if (targetsRemarks is not null && targetsRemarks.Length > 0)
            {
                foreach (var (target, remark) in targetsRemarks)
                {
                    AppendCode($"/// <param name=\"{target}\">{remark}</param>");
                }
            }
            if (!string.IsNullOrWhiteSpace(returns))
            {
                AppendCode($"/// <returns>{returns}</returns>");
            }
        }


        /// <summary>
        /// 插入 summarys 注释
        /// </summary>
        /// <param name="summary">摘要信息</param>
        /// <param name="targetsRemarks">参数备注信息</param>
        /// <param name="returns">返回值描述</param>
        public void Summarys(string summary, (string, string)[] targetsRemarks, string returns = null)
        {
            AppendCode($"/// <summary>");
            AppendCode($"/// {summary}");
            AppendCode($"/// </summary>");
            if (targetsRemarks is not null && targetsRemarks.Length > 0)
            {
                foreach (var (target, remark) in targetsRemarks)
                {
                    AppendCode($"/// <param name=\"{target}\">{remark}</param>");
                }
            }
            if (!string.IsNullOrWhiteSpace(returns))
            {
                AppendCode($"/// <returns>{returns}</returns>");
            }
        }

        /// <summary>
        /// 插入 summarys 注释
        /// </summary>
        /// <param name="summarys">摘要信息</param>
        public void Summarys(string[] summarys)
        {
            AppendCode($"/// <summary>");
            foreach (var summary in summarys)
            {
                AppendCode($"/// <para>{summary}</para>");
            }
            AppendCode($"/// </summary>");
        }

        /// <summary>
        /// 插入 summarys 注释
        /// </summary>
        /// <param name="summary">摘要信息</param>
        /// <param name="returns">返回值描述</param>
        public void Summarys(string summary, string returns = null)
        {
            AppendCode($"/// <summary>");
            AppendCode($"/// {summary}");
            AppendCode($"/// </summary>");
            if (!string.IsNullOrWhiteSpace(returns))
            {
                AppendCode($"/// <returns>{returns}</returns>");
            }
        }

        #endregion


        /// <summary>
        /// 生成 using 引用
        /// </summary>
        /// <returns></returns>
        internal GeneratorCache<T> GeneratorUsing() // where T : ClassCache
        {
            var generator = this;
#if true
           
            HashSet<string> namespaces = new HashSet<string>();
            foreach (var ns in GeneratorConfig.DefaultUsings)
            {
                if (namespaces.Contains(ns)) continue;
                namespaces.Add(ns);
                generator.AppendCode($"using {ns};");
            }

            if (generator.Cache is ClassCache classCache)
            {
                UsingNamespace(generator, namespaces, classCache);
            }
            else if (generator.Cache is IList classCaches)
            {
                foreach (var cache in classCaches)
                {
                    if (cache is ClassCache c)
                    {
                        UsingNamespace(generator, namespaces, c);
                    }
                }
            }
#endif
            return generator;

  
        }


        /// <summary>
        /// 引入命名空间
        /// </summary>
        /// <param name="generator"></param>
        /// <param name="namespaces"></param>
        /// <param name="classCache"></param>
        private static void UsingNamespace(GeneratorCache<T> generator, HashSet<string> namespaces, ClassCache classCache)
        {
            foreach (var usingCache in classCache.Namespaces)
            {
                var ns = usingCache.Namespace;
                if (namespaces.Contains(ns)) continue;
                namespaces.Add(ns);

                if (usingCache.IsStatic)
                {
                    
                    generator.AppendCode($"using static {ns}; ");
                }
                else if (usingCache.IsAlias)
                {

                }
                else
                {
                    generator.AppendCode($"using {ns};");
                }
            }

            /*var nns = classCache.Cache.GetAttr<HereinUsingAttribute>()?
                                       .GetMenber(nameof(HereinUsingAttribute.Namespace))?
                                       .GetValues() ?? new List<object>();

            foreach (var ns in nns.Select(x => x.ToString()))
            {
                if (namespaces.Contains(ns)) continue;
                namespaces.Add(ns);
                generator.AppendCode($"using {ns};");
            }*/
        }

    }
}
