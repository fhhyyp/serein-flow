
using Net462DllTest.Signal;
using Net462DllTest.Trigger;
using Net462DllTest.ViewModel;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.Framework.NodeFlow;
using Serein.Library.NodeFlow.Tool;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Net462DllTest.LogicControl
{




    [AutoRegister]
    [DynamicFlow("[View]")]
    public class ViewLogicControl
    {
        private readonly ViewManagement ViewManagement;
        public ViewLogicControl(ViewManagement ViewManagement)
        {
            this.ViewManagement = ViewManagement;
        }


        #region 触发器节点

        [NodeAction(NodeType.Flipflop, "等待视图命令")]
        public async Task<IFlipflopContext<int>> WaitTask(CommandSignal command)
        {
            (var type, var result) = await ViewManagement.CreateTaskWithTimeoutAsync(command, TimeSpan.FromHours(10), 0);
            if (type == TriggerType.Overtime)
            {
                return new FlipflopContext<int>(FlipflopStateType.Cancel, result);
            }
            else
            {

                return new FlipflopContext<int>(FlipflopStateType.Succeed, result);
            }

        }

        #endregion
        //[NodeAction(NodeType.Action, "打开窗体（指定枚举值）")]
        //public void OpenForm(IDynamicContext context,
        //                     FromValue fromId = FromValue.FromWorkBenchView,
        //                     bool isTop = true)
        //{
        //    var fromType = EnumHelper.GetBoundValue<FromValue, Type>(fromId, attr => attr.Value);
        //    if (fromType is null) return;
        //    if (context.Env.IOC.Instantiate(fromType) is Form form)
        //    {
        //        ViewManagement.OpenView(form, isTop);
        //    }
        //}

        [NodeAction(NodeType.Action, "打开窗体（转换器）")]
        public void OpenForm2([EnumTypeConvertor(typeof(FromValue))] Form form, bool isTop = true)
        {
            // 枚举转换为对应的Type并自动实例化
            ViewManagement.OpenView(form, isTop);
        }



        [NodeAction(NodeType.Action, "关闭指定类型的所有窗体")]
        public void CloseForm(IDynamicContext context, FromValue fromId = FromValue.FromWorkBenchView)
        {
            var fromType = EnumHelper.GetBoundValue<FromValue, Type>(fromId, attr => attr.Value);
            if (fromType is null) return;
            ViewManagement.CloseView(fromType);
        }





    }
}
