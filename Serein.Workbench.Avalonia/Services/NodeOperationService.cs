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
        internal NodeViewCreateEventArgs(INodeControl nodeControl, PositionOfUI position)
        {
            this.NodeControl = nodeControl;
            this.Position = position;
        }
        public INodeControl NodeControl { get; private set; }
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

            NodeMVVMManagement.RegisterUI(NodeControlType.Action, typeof(ActionNodeView), typeof(ActionNodeViewModel)); // 注册动作节点
            ConnectingData = new ConnectingData();
            feefService.OnNodeCreate += FeefService_OnNodeCreate; // 订阅运行环境创建节点事件


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

        public ConnectingData ConnectingData { get; private set; }
        public Canvas MainCanvas { get; set; }



        #region 私有变量

        /// <summary>
        /// 存储所有与节点有关的控件
        /// </summary>
        private Dictionary<string, INodeControl> NodeControls { get; } = [];



        /// <summary>
        /// 流程运行环境
        /// </summary>
        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 流程运行环境事件转发
        /// </summary>
        private readonly IFlowEEForwardingService feefService;
        #endregion

        /// <summary>
        /// 创建了节点控件
        /// </summary>
        public event NodeViewCreateHandle OnNodeViewCreate;

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
        /// 创建节点控件
        /// </summary>
        /// <param name="viewType">节点控件视图控件类型</param>
        /// <param name="viewModelType">节点控件ViewModel类型</param>
        /// <param name="nodeModel">节点Model实例</param>
        /// <param name="nodeView">返回的节点对象</param>
        /// <returns>是否创建成功</returns>
        /// <exception cref="Exception">无法创建节点控件</exception>
        private bool TryCreateNodeView(Type viewType, Type viewModelType, NodeModelBase nodeModel, out INodeControl? nodeView)
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
            if (controlObj is not INodeControl nodeControl)
            {
                nodeView = null;
                return false;
            }
            else
            {
                nodeControl.SetNodeModel(nodeModel);
                nodeView = nodeControl;
                return true;
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

        /// <summary>
        /// 尝试在连接控制点之间创建连接线
        /// </summary>
        public void TryCreateConnectionOnJunction(NodeJunctionView startJunction)
        {
            if (MainCanvas is not null)
            {
                var myData = ConnectingData;
                var junctionSize = startJunction.GetTransformedBounds()!.Value.Bounds.Size;
                var junctionPoint = new Point(junctionSize.Width / 2, junctionSize.Height / 2);
                if (startJunction.TranslatePoint(junctionPoint, MainCanvas) is Point point)
                {
                    myData.StartPoint = point;
                }
                else
                {
                    return;
                }

                myData.Reset();
                myData.IsCreateing = true; // 表示开始连接
                myData.StartJunction = startJunction;
                myData.CurrentJunction = startJunction;

                var junctionOfConnectionType = startJunction.JunctionType.ToConnectyionType();
                ConnectionLineShape bezierLine; // 类别
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

                //Mouse.OverrideCursor = Cursors.Cross; // 设置鼠标为正在创建连线
                myData.TempLine = new MyLine(MainCanvas, bezierLine);
            }
        }
    }

}
