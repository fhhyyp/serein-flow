using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using Serein.NodeFlow.Services;
using System.Text;

namespace Serein.NodeFlow.Model.Infos
{
    /// <summary>
    /// 指示流程接口方法需要生成什么代码
    /// </summary>
    internal class FlowApiMethodInfo
    {

        public FlowApiMethodInfo(SingleFlowCallNode singleFlowCallNode)
        {
            NodeModel = singleFlowCallNode;
        }

        /// <summary>
        /// 对应的流程节点
        /// </summary>
        public SingleFlowCallNode NodeModel { get; }  

        /// <summary>
        /// 生成的接口名称
        /// </summary>
        public string ApiMethodName { get; set; }

        /// <summary>
        /// 返回类型
        /// </summary>
        public Type ReturnType { get; set; }

        /// <summary>
        /// 参数实体信息
        /// </summary>
        public List<ParamInfo> ParamInfos { get; set; } = [];

        /// <summary>
        /// 参数实体类型名称
        /// </summary>
        public string ParamTypeName => $"FlowApiInvoke_{ApiMethodName}";

        public class ParamInfo
        {
            public ParamInfo(Type type, string paramName)
            {
                Type = type;
                ParamName = paramName;
            }

            /// <summary>
            /// 
            /// </summary>
            public Type Type { get; set; }
            /// <summary>
            /// 参数名称
            /// </summary>
            public string ParamName { get; set; }
            /// <summary>
            /// 注释备注
            /// </summary>
            public string Comments { get; set; }
        }


        public enum ParamType
        {
            Defute, // 仅使用方法参数
            HasToken, // 包含取消令牌和方法参数
            HasContextAndToken, // 包含上下文、取消令牌和方法参数
        }


        public bool IsVoid => ReturnType == typeof(void);
        public string ObjPoolName => $"_objPool{ParamTypeName.ToPascalCase()}";
        /// <summary>
        /// 生成所需的参数类的签名代码
        /// </summary>
        /// <returns></returns>
        public string ToParamterClassSignature()
        {
            StringBuilder sb = new StringBuilder();


            //sb.AppendCode(2, $"private static readonly global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}> {ObjPoolName} = ");
            //sb.AppendCode(2, $"    new global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}>(");
            //sb.AppendCode(2, $"         new global::Microsoft.Extensions.ObjectPool.DefaultPooledObjectPolicy<{ParamTypeName}>()); ");
            sb.AppendLine();
            var classXmlComments = $"流程接口[{ApiMethodName}]需要的参数类".ToXmlComments(2);
            sb.AppendCode(2, classXmlComments);
            sb.AppendCode(2, $"public class {ParamTypeName}");
            sb.AppendCode(2, $"{{");
            for (int index = 0; index < ParamInfos.Count; index++)
            {
                ParamInfo? info = ParamInfos[index];
                var argXmlComments = $"[{index}]流程接口参数{(string.IsNullOrWhiteSpace(info.Comments) ? string.Empty : $"，{info.Comments}。")}".ToXmlComments(2);
                sb.AppendCode(3, argXmlComments);
                sb.AppendCode(3, $"public global::{info.Type.FullName} {info.ParamName.ToPascalCase()} {{ get; set; }}");
            }
            sb.AppendCode(2, $"}}");
            return sb.ToString();
        }

        public string ToObjPoolSignature()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendCode(2, $"private static readonly global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}> {ObjPoolName} = ");
            sb.AppendCode(4, $"new global::Microsoft.Extensions.ObjectPool.DefaultObjectPool<{ParamTypeName}>(");
            sb.AppendCode(5, $"new global::Microsoft.Extensions.ObjectPool.DefaultPooledObjectPolicy<{ParamTypeName}>()); ");
            return sb.ToString();
        }

        /// <summary>
        /// 生成接口的签名方法
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string ToInterfaceMethodSignature(ParamType type)
        {
            var taskTypeFullName = $"global::System.Threading.Tasks.Task";
            var contextFullName = $"global::{typeof(IFlowContext).FullName}";
            var tokenFullName = $"global::{typeof(CancellationToken).FullName}";
            var returnContext = IsVoid ? taskTypeFullName : $"{taskTypeFullName}<{ReturnType.FullName}>";
            if (type == ParamType.Defute)
            {
                var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                return $"{returnContext} {ApiMethodName}({paramSignature});";
            }
            else if (type == ParamType.HasToken)
            {
                var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                return $"{returnContext}  {ApiMethodName}({tokenFullName} token, {paramSignature});";
            }
            else if (type == ParamType.HasContextAndToken)
            {
                var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                return $"{returnContext}  {ApiMethodName}({contextFullName} flowContext, {tokenFullName} token, {paramSignature});";
            }
            throw new Exception();
        }

        /// <summary>
        /// 生成实现方法的签名代码
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public string ToImpleMethodSignature(ParamType type)
        {
            var taskTypeFullName = $"global::System.Threading.Tasks.Task";
            var contextApiFullName = $"global::{typeof(IFlowContext).FullName}";
            var contextImpleFullName = $"global::{typeof(FlowContext).FullName}";
            var tokenSourceFullName = $"global::{typeof(CancellationTokenSource).FullName}";
            var tokenFullName = $"global::{typeof(CancellationToken).FullName}";
            var flowContextPoolName = $"global::{typeof(LightweightFlowControl).FullName}";
            string flowEnvironment = nameof(flowEnvironment);
            string flowContext = nameof(flowContext);
            string token = nameof(token);

            var returnTypeContext = IsVoid ? taskTypeFullName : $"{taskTypeFullName}<{ReturnType.FullName}>";

            StringBuilder sb = new StringBuilder();
            if (IsVoid)
            {
                if (type == ParamType.Defute)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{{{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"{tokenSourceFullName} cts = new {tokenSourceFullName}(); // 创建取消令牌");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,        $"{{");
                    sb.AppendCode(4,        $"await {ApiMethodName}({flowContext}, cts.Token, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"cts.{nameof(CancellationTokenSource.Dispose)}(); ");
                    sb.AppendCode(4,        $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{{{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({tokenFullName} {token}, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"await {ApiMethodName}({flowContext}, {token}, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(4,        $"{flowContext}.{nameof(IFlowContext.Reset)}(); ");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasContextAndToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{{{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));


                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({contextApiFullName} {flowContext}, {tokenFullName} token, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"token.ThrowIfCancellationRequested(); // 检查任务是否取消");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(3,        $"global::{ParamTypeName} data = {ObjPoolName}.Get(); // 从对象池获取一个对象");
                    for (int index = 0; index < ParamInfos.Count; index++)
                    {
                        ParamInfo? info = ParamInfos[index];
                        sb.AppendCode(4,    $"data.{info.ParamName.ToPascalCase()} = {info.ParamName}; // [{index}] {info.Comments}");
                    }
                    sb.AppendCode(3,        $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{ApiMethodName}\", data);");
                    sb.AppendCode(3,        $"{flowContext}.{nameof(IFlowContext.SetPreviousNode)}(\"{NodeModel.TargetNode.Guid}\", \"{ApiMethodName}\");");
                    sb.AppendCode(3,        $"global::{typeof(CallNode).FullName} node = Get(\"{NodeModel.Guid}\");");
                    sb.AppendCode(3,        $"await node.{nameof(CallNode.StartFlowAsync)}({flowContext}, {token}); // 调用目标方法");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
            }
            else
            {
                string flowResult = nameof(flowResult);
                if (type == ParamType.Defute)
                {
                    //sb.AppendCode(3, $"{contextApiFullName} {flowContext} = new {contextImpleFullName}({flowEnvironment}); // 创建上下文");

                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"{tokenSourceFullName} cts = new {tokenSourceFullName}(); // 创建取消令牌");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{ReturnType.FullName} {flowResult} =  await {ApiMethodName}({flowContext}, cts.{nameof(CancellationTokenSource.Token)}, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(4,       $"return {flowResult};");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"cts.{nameof(CancellationTokenSource.Dispose)}(); ");
                    sb.AppendCode(4,       $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({tokenFullName} {token}, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"{contextApiFullName} {flowContext} = {flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Allocate)}(); // 从对象池获取一个上下文");
                    sb.AppendCode(3,    $"try");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{ReturnType.FullName} {flowResult} = await {ApiMethodName}({flowContext}, {token}, {invokeParamSignature}); // 调用目标方法");
                    sb.AppendCode(4,       $"return {flowResult};");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"catch (Exception)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"throw;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"finally");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,       $"{flowContextPoolName}.{nameof(LightweightFlowControl.FlowContextPool)}.{nameof(LightweightFlowControl.FlowContextPool.Free)}({flowContext}); // 释放上下文");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                }
                else if (type == ParamType.HasContextAndToken)
                {
                    var paramSignature = string.Join(", ", ParamInfos.Select(p => $"global::{p.Type.FullName} {p.ParamName}"));
                    var invokeParamSignature = string.Join(", ", ParamInfos.Select(p => p.ParamName));
                    sb.AppendCode(2, $"public async {returnTypeContext} {ApiMethodName}({contextApiFullName} {flowContext}, {tokenFullName} token, {paramSignature})");
                    sb.AppendCode(2, $"{{");
                    sb.AppendCode(3,    $"token.ThrowIfCancellationRequested(); // 检查任务是否取消");
                    sb.AppendCode(3,    $"global::{ParamTypeName} data = {ObjPoolName}.Get(); // 从对象池获取一个对象");
                    for (int index = 0; index < ParamInfos.Count; index++)
                    {
                        ParamInfo? info = ParamInfos[index];
                        sb.AppendCode(4, $"data.{info.ParamName.ToPascalCase()} = {info.ParamName}; // [{index}] {info.Comments}"); // 进行赋值
                    }
                    sb.AppendCode(3,    $"{flowContext}.{nameof(IFlowContext.AddOrUpdate)}(\"{ApiMethodName}\", data);");
                    sb.AppendCode(3,    $"{flowContext}.{nameof(IFlowContext.SetPreviousNode)}(\"{NodeModel.Guid}\", \"{ApiMethodName}\");");
                    sb.AppendCode(3,    $"global::{typeof(CallNode).FullName} node = Get(\"{NodeModel.Guid}\");");
                    sb.AppendCode(3,    $"global::{typeof(FlowResult).FullName} {flowResult} = await node.{nameof(CallNode.StartFlowAsync)}({flowContext}, {token}); // 调用目标方法");
                    if(ReturnType == typeof(object))
                    {
                        sb.AppendCode(3, $"if ({flowResult}.{nameof(FlowResult.Value)} is null)");
                        sb.AppendCode(3, $"{{");
                        sb.AppendCode(4, $"return null;");
                        sb.AppendCode(3, $"}}");
                        sb.AppendCode(3, $"else", isWrapping :false);
                    }
                    sb.AppendCode(3,    $"if ({flowResult}.{nameof(FlowResult.Value)} is global::{ReturnType.FullName} result)");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"return result;");
                    sb.AppendCode(3,    $"}}");
                    sb.AppendCode(3,    $"else");
                    sb.AppendCode(3,    $"{{");
                    sb.AppendCode(4,        $"throw new ArgumentNullException($\"类型转换失败，{{(flowResult.Value is null ? \"返回数据为 null\" : $\"返回数据与需求类型不匹配，当前返回类型为[{{flowResult.Value.GetType().FullName}}。\")}}\");");
                    sb.AppendCode(3,    $"}}");
                    //sb.AppendCode(3,    $"return {flowResult};");
                    sb.AppendCode(2, $"}}");
                    return sb.ToString();
                    // throw new ArgumentNullException($"类型转换失败，{(flowResult.Value is null ? "返回数据为 null" : $"返回数据与需求类型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}。")}");
                }
            }

            throw new Exception();

        }
    }



}
