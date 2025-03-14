using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Api
{

    /// <summary>
    /// 流程中的控件
    /// </summary>
    public interface IFlowControl
    {
        /// <summary>
        /// 节点执行事件
        /// </summary>
        void OnExecuting(object data);

    }

    /// <summary>
    /// 自定义UI显示
    /// </summary>
    public interface IEmbeddedContent
    {
        /// <summary>
        /// 获取用户控件（WPF）
        /// </summary>
        object GetUserControl();

        /// <summary>
        /// 获取窗体控件
        /// </summary>
        /// <returns></returns>
        IFlowControl GetFlowControl();

   
    }

}
