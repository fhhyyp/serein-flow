using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Newtonsoft.Json;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Avalonia.Custom.Node.ViewModels;
using Serein.Workbench.Avalonia.Custom.Node.Views;
using Serein.Workbench.Avalonia.Custom.Views;
using Serein.Workbench.Avalonia.Extension;
using Serein.Workbench.Avalonia.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;





namespace Serein.Workbench.Avalonia.Api
{

    /// <summary>
    /// 提供节点操作的接口
    /// </summary>
    internal interface INodeOperationService
    {
        /// <summary>
        /// 连接数据
        /// </summary>
        ConnectingData ConnectingData { get; }

        /// <summary>
        /// 主画布
        /// </summary>
        Canvas MainCanvas { get; set; }

        /// <summary>
        /// 节点创建事件
        /// </summary>

        event NodeViewCreateHandle OnNodeViewCreate;

        /// <summary>
        /// 创建节点控件
        /// </summary>
        /// <param name="nodeType">控件类型</param>
        /// <param name="position">创建坐标</param>
        /// <param name="methodDetailsInfo">节点方法信息</param>
        public void CreateNodeView(MethodDetailsInfo methodDetailsInfo, PositionOfUI position);

        /// <summary>
        /// 尝试从连接控制点创建连接
        /// </summary>
        /// <param name="startJunction"></param>
        void TryCreateConnectionOnJunction(NodeJunctionView startJunction);

    }




    #region 事件与事件参数
    /// <summary>
    /// 创建节点控件事件
    /// </summary>
    /// <param name="eventArgs"></param>

    internal delegate bool NodeViewCreateHandle(NodeViewCreateEventArgs eventArgs);

    /// <summary>
    /// 创建节点控件事件参数
    /// </summary>



    internal class NodeViewCreateEventArgs : EventArgs
    {
        internal NodeViewCreateEventArgs(NodeControlBase nodeControl, PositionOfUI position)
        {
            this.NodeControl = nodeControl;
            this.Position = position;
        }
        public NodeControlBase NodeControl { get; private set; }
        public PositionOfUI Position { get; private set; }
    }


    #endregion





}

namespace Serein.Workbench.Avalonia.Services
{
    /// <summary>
    /// 节点操作相关服务
    /// </summary>
    internal class NodeOperationService : INodeOperationService
    {

        public NodeOperationService(IFlowEnvironment flowEnvironment,
                                    IFlowEEForwardingService feefService)
        {
            this.flowEnvironment = flowEnvironment;
            this.feefService = feefService;
            feefService.OnNodeCreate += FeefService_OnNodeCreate; // 订阅运行环境创建节点事件
            feefService.OnNodeConnectChange += FeefService_OnNodeConnectChange; // 订阅运行环境连接了节点事件
            NodeMVVMManagement.RegisterUI(NodeControlType.Action, typeof(ActionNodeView), typeof(ActionNodeViewModel)); // 注册动作节点

            // 手动加载项目
            _ = Task.Run(async delegate
            {
                await Task.Delay(1000);
                var flowEnvironment = App.GetService<IFlowEnvironment>();
                var filePath = @"C:\Users\Az\source\repos\CLBanyunqiState\CLBanyunqiState\bin\debug\net8.0\project.dnf";
                string content = System.IO.File.ReadAllText(filePath); // 读取整个文件内容
                var projectData = JsonConvert.DeserializeObject<SereinProjectData>(content);
                var projectDfilePath = System.IO.Path.GetDirectoryName(filePath)!;
                flowEnvironment.LoadProject(new FlowEnvInfo { Project = projectData }, projectDfilePath);
            }, CancellationToken.None);


        }


        #region 接口属性
        public ConnectingData ConnectingData { get; private set; } = new ConnectingData();
        public Canvas MainCanvas { get; set; }

        #endregion

        #region 私有变量

        /// <summary>
        /// 存储所有与节点有关的控件
        /// </summary>
        private Dictionary<string, NodeControlBase> NodeControls { get; } = [];

        /// <summary>
        /// 存储所有连接
        /// </summary>
        private List<NodeConnectionLineView> Connections { get; } = [];



        /// <summary>
        /// 流程运行环境
        /// </summary>
        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 流程运行环境事件转发
        /// </summary>
        private readonly IFlowEEForwardingService feefService;
        #endregion

        #region 节点操作事件

        /// <summary>
        /// 创建了节点控件
        /// </summary>
        public event NodeViewCreateHandle OnNodeViewCreate;

        #endregion

        #region 转发事件的处理

        /// <summary>
        /// 从工作台事件转发器监听节点创建事件
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FeefService_OnNodeCreate(NodeCreateEventArgs eventArgs)
        {
            var nodeModel = eventArgs.NodeModel;
            if (NodeControls.ContainsKey(nodeModel.Guid))
            {
                SereinEnv.WriteLine(InfoType.WARN, $"OnNodeCreate 事件意外触发，节点Guid重复 - {nodeModel.Guid}");
                return;
            }
            if (!NodeMVVMManagement.TryGetType(nodeModel.ControlType, out var nodeMVVM))
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModel.ControlType}节点，节点类型尚未注册。");
                return;
            }
            if (nodeMVVM.ControlType == null
                || nodeMVVM.ViewModelType == null)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModel.ControlType}节点，UI类型尚未注册（请通过 NodeMVVMManagement.RegisterUI() 方法进行注册）。");
                return;
            }

            var isSuccessful = TryCreateNodeView(nodeMVVM.ControlType, // 控件UI类型
                                                nodeMVVM.ViewModelType, // 控件VIewModel类型
                                                nodeModel, // 控件数据实体
                                                out var nodeControl); // 成功创建后传出的节点控件实体
            if (!isSuccessful || nodeControl is null)
            {
                SereinEnv.WriteLine(InfoType.INFO, $"无法创建{nodeModel.ControlType}节点，节点创建失败。");
                return;
            }


            var e = new NodeViewCreateEventArgs(nodeControl, eventArgs.Position);
            if (OnNodeViewCreate?.Invoke(e) == true)
            {
                // 成功创建
                NodeControls.TryAdd(nodeModel.Guid, nodeControl); // 缓存起来，通知其它地方拿取这个控件
            }

        }


        /// <summary>
        /// 运行环境连接了节点事件
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FeefService_OnNodeConnectChange(NodeConnectChangeEventArgs eventArgs)
        {
#if false
            string fromNodeGuid = eventArgs.FromNodeGuid;
            string toNodeGuid = eventArgs.ToNodeGuid;
            if (!TryGetControl(fromNodeGuid, out var fromNodeControl)
               || !TryGetControl(toNodeGuid, out var toNodeControl))
            {
                return;
            }

            if (eventArgs.JunctionOfConnectionType == JunctionOfConnectionType.Invoke)
            {
                ConnectionInvokeType connectionType = eventArgs.ConnectionInvokeType;
                #region 创建/删除节点之间的调用关系
                #region 创建连接
                if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Create) // 添加连接
                {
                    if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
                    {
                        SereinEnv.WriteLine(InfoType.INFO, "非预期的连接");
                        return;
                    }
                    var startJunction = IFormJunction.NextStepJunction;
                    var endJunction = IToJunction.ExecuteJunction;

                    startJunction.TransformToVisual(MainCanvas);

                    // 添加连接
                    var shape = new ConnectionLineShape(
                        FlowChartCanvas,
                        connectionType,
                        startJunction,
                        endJunction
                    );
                    NodeConnectionLine nodeConnectionLine = new NodeConnectionLine(MainCanvas, shape);

                    //if (toNodeControl is FlipflopNodeControl flipflopControl
                    //    && flipflopControl?.ViewModel?.NodeModel is NodeModelBase nodeModel) // 某个节点连接到了触发器，尝试从全局触发器视图中移除该触发器
                    //{
                    //    NodeTreeViewer.RemoveGlobalFlipFlop(nodeModel); // 从全局触发器树树视图中移除
                    //}

                    Connections.Add(nodeConnectionLine);
                    fromNodeControl.AddCnnection(shape);
                    toNodeControl.AddCnnection(shape);
                }
                #endregion
#if false

                #region 移除连接
                else if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Remove) // 移除连接
                {
                    // 需要移除连接
                    var removeConnections = Connections.Where(c =>
                                               c.Start.MyNode.Guid.Equals(fromNodeGuid)
                                            && c.End.MyNode.Guid.Equals(toNodeGuid)
                                            && (c.Start.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke
                                            || c.End.JunctionType.ToConnectyionType() == JunctionOfConnectionType.Invoke))
                                            .ToList();


                    foreach (var connection in removeConnections)
                    {
                        Connections.Remove(connection);
                        fromNodeControl.RemoveConnection(connection); // 移除连接
                        toNodeControl.RemoveConnection(connection); // 移除连接
                        if (NodeControls.TryGetValue(connection.End.MyNode.Guid, out var control))
                        {
                            JudgmentFlipFlopNode(control); // 连接关系变更时判断
                        }
                    }
                }
                #endregion

#endif
                #endregion
            }
            else
            {
 #if false
		ConnectionArgSourceType connectionArgSourceType = eventArgs.ConnectionArgSourceType;
                 #region 创建/删除节点之间的参数传递关系
                 #region 创建连接
                 if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Create) // 添加连接
                 {
                     if (fromNodeControl is not INodeJunction IFormJunction || toNodeControl is not INodeJunction IToJunction)
                     {
                         SereinEnv.WriteLine(InfoType.INFO, "非预期的情况");
                         return;
                     }

                     JunctionControlBase startJunction = eventArgs.ConnectionArgSourceType switch
                     {
                         ConnectionArgSourceType.GetPreviousNodeData => IFormJunction.ReturnDataJunction, // 自身节点
                         ConnectionArgSourceType.GetOtherNodeData => IFormJunction.ReturnDataJunction, // 其它节点的返回值控制点
                         ConnectionArgSourceType.GetOtherNodeDataOfInvoke => IFormJunction.ReturnDataJunction, // 其它节点的返回值控制点
                         _ => throw new Exception("窗体事件 FlowEnvironment_NodeConnectChangeEvemt 创建/删除节点之间的参数传递关系 JunctionControlBase 枚举值错误 。非预期的枚举值。") // 应该不会触发
                     };

                     if (IToJunction.ArgDataJunction.Length <= eventArgs.ArgIndex)
                     {
                         _ = Task.Run(async () =>
                         {
                             await Task.Delay(500);
                             FlowEnvironment_NodeConnectChangeEvemt(eventArgs);
                         });
                         return;
                     }
                     JunctionControlBase endJunction = IToJunction.ArgDataJunction[eventArgs.ArgIndex];
                     LineType lineType = LineType.Bezier;
                     // 添加连接
                     var connection = new ConnectionControl(
                         lineType,
                         FlowChartCanvas,
                         eventArgs.ArgIndex,
                         eventArgs.ConnectionArgSourceType,
                         startJunction,
                         endJunction,
                         IToJunction
                     );
                     Connections.Add(connection);
                     fromNodeControl.AddCnnection(connection);
                     toNodeControl.AddCnnection(connection);
                     EndConnection(); // 环境触发了创建节点连接事件


                 }
                 #endregion
                 #region 移除连接
                 else if (eventArgs.ChangeType == NodeConnectChangeEventArgs.ConnectChangeType.Remove) // 移除连接
                 {
                     // 需要移除连接
                     var removeConnections = Connections.Where(c => c.Start.MyNode.Guid.Equals(fromNodeGuid)
                                                                     && c.End.MyNode.Guid.Equals(toNodeGuid))
                                                                 .ToList(); // 获取这两个节点之间的所有连接关系



                     foreach (var connection in removeConnections)
                     {
                         if (connection.End is ArgJunctionControl junctionControl && junctionControl.ArgIndex == eventArgs.ArgIndex)
                         {
                             // 找到符合删除条件的连接线
                             Connections.Remove(connection); // 从本地记录中移除
                             fromNodeControl.RemoveConnection(connection); // 从节点持有的记录移除
                             toNodeControl.RemoveConnection(connection); // 从节点持有的记录移除
                         }


                         //if (NodeControls.TryGetValue(connection.End.MyNode.Guid, out var control))
                         //{
                         //    JudgmentFlipFlopNode(control); // 连接关系变更时判断
                         //}
                     }
                 }
                 #endregion
                 #endregion 
#endif
            } 
#endif
        }
        #endregion

        #region 私有方法

        /// <summary>
        /// 创建节点控件
        /// </summary>
        /// <param name="viewType">节点控件视图控件类型</param>
        /// <param name="viewModelType">节点控件ViewModel类型</param>
        /// <param name="nodeModel">节点Model实例</param>
        /// <param name="nodeView">返回的节点对象</param>
        /// <returns>是否创建成功</returns>
        /// <exception cref="Exception">无法创建节点控件</exception>
        private bool TryCreateNodeView(Type viewType, Type viewModelType, NodeModelBase nodeModel, out NodeControlBase? nodeView)
        {
            if (string.IsNullOrEmpty(nodeModel.Guid))
            {
                nodeModel.Guid = Guid.NewGuid().ToString();
            }
            var t_ViewModel = Activator.CreateInstance(viewModelType);
            if (t_ViewModel is not NodeViewModelBase viewModelBase)
            {
                nodeView = null;
                return false;
            }
            viewModelBase.NodeModelBase = nodeModel; // 设置节点对象
            var controlObj = Activator.CreateInstance(viewType);
            if (controlObj is NodeControlBase nodeControl)
            {
                nodeControl.DataContext = viewModelBase;
                nodeView = nodeControl;
                return true;
            }
            else
            {
                nodeView = null;
                return false;
            }

            // 在其它地方验证过了，所以注释
            //if ((viewType is null)
            //    || viewModelType is null
            //    || nodeModel is null)
            //{
            //    nodeView = null;
            //    return false;
            //}
            //if (typeof(INodeControl).IsSubclassOf(viewType) 
            // || typeof(NodeViewModelBase).IsSubclassOf(viewModelType))
            //{
            //    nodeView = null;
            //    return false;
            //}
        }

        private bool TryGetControl(string nodeGuid, out NodeControlBase nodeControl)
        {
            if (string.IsNullOrEmpty(nodeGuid))
            {
                nodeControl = null;
                return false;
            }
            if (!NodeControls.TryGetValue(nodeGuid, out nodeControl))
            {
                nodeControl = null;
                return false;
            }
            if (nodeControl is null)
            {
                return false;
            }
            return true;
        }

        #endregion

        #region 操作接口对外暴露的接口

        /// <summary>
        /// 创建节点控件
        /// </summary>
        /// <param name="nodeType">控件类型</param>
        /// <param name="position">创建坐标</param>
        /// <param name="methodDetailsInfo">节点方法信息（基础节点传null）</param>
        public void CreateNodeView(MethodDetailsInfo methodDetailsInfo, PositionOfUI position)
        {
            Task.Run(async () =>
            {
                if (EnumHelper.TryConvertEnum<NodeControlType>(methodDetailsInfo.NodeType, out var nodeType))
                {
                    await flowEnvironment.CreateNodeAsync(nodeType, position, methodDetailsInfo);
                }
            });
        }


        /// <summary>
        /// 尝试在连接控制点之间创建连接线
        /// </summary>
        public void TryCreateConnectionOnJunction(NodeJunctionView startJunction)
        {
            if (MainCanvas is not null)
            {
                ConnectingData.Reset();
                ConnectingData.IsCreateing = true; // 表示开始连接
                ConnectingData.StartJunction = startJunction;
                ConnectingData.CurrentJunction = startJunction;
                if(startJunction.JunctionType == JunctionType.NextStep || startJunction.JunctionType == JunctionType.ReturnData)
                {

                    ConnectingData.TempLine = new NodeConnectionLineView(MainCanvas, startJunction, null);
                }
                else
                {
                    ConnectingData.TempLine = new NodeConnectionLineView(MainCanvas,null ,startJunction);
                }


                /*var junctionOfConnectionType = startJunction.JunctionType.ToConnectyionType();
                ConnectionLineShape bezierLine; 
                Brush brushColor; // 临时线的颜色
                if (junctionOfConnectionType == JunctionOfConnectionType.Invoke)
                {
                    brushColor = ConnectionInvokeType.IsSucceed.ToLineColor();
                }
                else if (junctionOfConnectionType == JunctionOfConnectionType.Arg)
                {
                    brushColor = ConnectionArgSourceType.GetOtherNodeData.ToLineColor();
                }
                else
                {
                    return;
                }
                bezierLine = new ConnectionLineShape(myData.StartPoint,
                                                     myData.StartPoint,
                                                     brushColor,
                                                     isTop: true); // 绘制临时的线
                */
                //Mouse.OverrideCursor = Cursors.Cross; // 设置鼠标为正在创建连线

            }
        } 

        #endregion
    }

}
