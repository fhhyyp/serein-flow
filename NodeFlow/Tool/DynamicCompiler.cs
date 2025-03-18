using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Tool
{
    /// <summary>
    /// 动态编译
    /// </summary>
    public class DynamicCompiler
    {
        private readonly HashSet<MetadataReference> _references = new HashSet<MetadataReference>();

        public DynamicCompiler()
        {
            // 默认添加当前 AppDomain 加载的所有程序集
            var defaultReferences = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !string.IsNullOrEmpty(a.Location)) // a.IsDynamic  动态程序集
                .Select(a => MetadataReference.CreateFromFile(a.Location));


            //AddReference(this.GetType());
            _references.UnionWith(defaultReferences);
        }

        /// <summary>
        /// 添加依赖程序集（通过类型）
        /// </summary>
        /// <param name="type">类型所在的程序集</param>
        public void AddReference(Type type)
        {
            var assemblyLocation = type.Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                _references.Add(MetadataReference.CreateFromFile(assemblyLocation));
            }
        }

        /// <summary>
        /// 添加依赖程序集（通过文件路径）
        /// </summary>
        /// <param name="assemblyPath">程序集文件路径</param>
        public void AddReference(string assemblyPath)
        {
            if (File.Exists(assemblyPath))
            {
                _references.Add(MetadataReference.CreateFromFile(assemblyPath));
            }
        }

        /// <summary>
        /// 编译 C# 代码并返回程序集
        /// </summary>
        /// <param name="code">C# 代码文本</param>
        /// <param name="assemblyName">程序集名称（可选）</param>
        /// <returns>成功返回 Assembly，失败返回 null</returns>
        public Assembly Compile(string code, string assemblyName = null)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(code);
            if (assemblyName is null)
            {
                assemblyName = Path.GetRandomFileName(); // 生成随机程序集名称

            }

            var temp_dir = Path.Combine(Directory.GetCurrentDirectory(), "temp");
            if (!Directory.Exists(temp_dir))
            {
                Directory.CreateDirectory(temp_dir);
            }
            var savePath = Path.Combine(temp_dir, $"{assemblyName}.dll");

            var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);

            
            CSharpCompilation compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                _references,
               options

            );

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    Console.WriteLine("编译失败：");
                    foreach (var diagnostic in result.Diagnostics)
                    {
                        Console.WriteLine(diagnostic.ToString());
                    }
                    return null;
                }
                
                ms.Seek(0, SeekOrigin.Begin);
                var t12 = AppContext.BaseDirectory;
                var assembly = Assembly.Load(ms.ToArray());
                var t1 = assembly.Location;
                var t = assembly.GetType().Assembly.Location;

                // 保存
                
                compilation.Emit(savePath);
                return assembly;
            }

        }

        public void Save()
        {
            
        }



    }
}
