using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程编辑
    /// </summary>
    public interface IFlowEdit
    {

        /// <summary>
        /// 节点视图模型管理类
        /// </summary>
        NodeMVVMService NodeMVVMManagement { get; }

        /// <summary>
        /// 从节点信息集合批量加载节点控件
        /// </summary>
        /// <param name="nodeInfos">节点集合信息</param>
        /// <returns></returns>
        Task LoadNodeInfosAsync(List<NodeInfo> nodeInfos);

        #region 流程节点操作接口

        /// <summary>
        /// 增加画布
        /// </summary>
        /// <param name="canvasName">画布名称</param>
        /// <param name="width">宽度</param>
        /// <param name="height">高度</param>
        /// <returns></returns>
        void CreateCanvas(string canvasName, int width, int height);

        /// <summary>
        /// 删除画布
        /// </summary>
        /// <param name="canvasGuid">画布Guid</param>
        /// <returns></returns>
        void RemoveCanvas(string canvasGuid);




        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="invokeType">决定了方法执行后的后继行为</param>
        void ConnectInvokeNode(string canvasGuid,
                                          string fromNodeGuid,
                                          string toNodeGuid,
                                          JunctionType fromNodeJunctionType,
                                          JunctionType toNodeJunctionType,
                                          ConnectionInvokeType invokeType);

        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="fromNodeJunctionType">起始节点控制点</param>
        /// <param name="toNodeJunctionType">目标节点控制点</param>
        /// <param name="argSourceType">决定了方法参数来源</param>
        /// <param name="argIndex">设置第几个参数</param>
        void ConnectArgSourceNode(string canvasGuid,
                                  string fromNodeGuid,
                                  string toNodeGuid,
                                  JunctionType fromNodeJunctionType,
                                  JunctionType toNodeJunctionType,
                                  ConnectionArgSourceType argSourceType,
                                  int argIndex);

        /// <summary>
        /// 移除两个节点之间的方法调用关系
        /// </summary>        
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接类型</param>
        void RemoveInvokeConnect(string canvasGuid, string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType);

        /// <summary>
        /// 移除连接节点之间参数传递的关系
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="argIndex">连接到第几个参数</param>
        void RemoveArgSourceConnect(string canvasGuid, string fromNodeGuid, string toNodeGuid, int argIndex);


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="nodeType">控件类型</param>
        /// <param name="position">节点在画布上的位置（</param>
        /// <param name="methodDetailsInfo">节点绑定的方法说明</param>
        void CreateNode(string canvasGuid, NodeControlType nodeType, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null);

        /// <summary>
        /// 移除节点
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="nodeGuid">待移除的节点Guid</param>
        void RemoveNode(string canvasGuid, string nodeGuid);

        /// <summary>
        ///  将节点放置在容器中
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="nodeGuid">需要放置的节点Guid</param>
        /// <param name="containerNodeGuid">存放节点的容器Guid</param>
        /// <returns></returns>
        void PlaceNodeToContainer(string canvasGuid, string nodeGuid, string containerNodeGuid);

        /// <summary>
        ///  将节点放置在容器中
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="nodeGuid">需要取出的节点Guid</param>
        void TakeOutNodeToContainer(string canvasGuid, string nodeGuid);

        /// <summary>
        /// 设置流程起点节点
        /// </summary>
        /// <param name="canvasGuid">所在画布</param>
        /// <param name="nodeGuid">尝试设置为起始节点的节点Guid</param>
        /// <returns>被设置为起始节点的Guid</returns>
        void SetStartNode(string canvasGuid, string nodeGuid);

        /// <summary>
        /// 设置两个节点某个类型的方法调用关系为优先调用
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接关系</param>
        /// <returns></returns>
        void SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType);


        /// <summary>
        /// 改变可选参数的数目
        /// </summary>
        /// <param name="nodeGuid">对应的节点Guid</param>
        /// <param name="isAdd">true，增加参数；false，减少参数</param>
        /// <param name="paramIndex">以哪个参数为模板进行拷贝，或删去某个参数（该参数必须为可选参数）</param>
        /// <returns></returns>
        void ChangeParameter(string nodeGuid, bool isAdd, int paramIndex);

        #endregion

        #region UI视觉

        /// <summary>
        /// 节点定位
        /// </summary>
        /// <param name="nodeGuid"></param>
        void NodeLocate(string nodeGuid);

        #endregion
    }
}
