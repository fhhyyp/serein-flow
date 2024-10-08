
using Net462DllTest.Signal;
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

    /// <summary>
    /// 视图管理
    /// </summary>
    [AutoRegister]
    public class ViewManagement:ChannelFlowTrigger<CommandSignal>
    {
        private readonly List<Form> forms = new List<Form>();
        /// <summary>
        /// 打开窗口
        /// </summary>
        /// <param name="form">要打开的窗口类型</param>
        /// <param name="isTop">是否置顶</param>
        public void OpenView(Form form, bool isTop)
        {
            form.TopMost = isTop;
            form.Show();
            forms.Add(form);
        }
        public void CloseView(Type formType)
        {
             var remoteForms =  forms.Where(f => f.GetType() == formType).ToArray();
            foreach (Form f in remoteForms)
            {
                f.Close();
                f.Dispose();
                this.forms.Remove(f);
            }
        }
    }


   

    [DynamicFlow("[View]")]
    public class ViewLogicControl
    {
        private readonly ViewManagement ViewManagement;
        public ViewLogicControl(ViewManagement ViewManagement)
        {
            this.ViewManagement = ViewManagement;
        }


        #region 触发器节点

        [NodeAction(NodeType.Flipflop, "等待视图命令", ReturnType = typeof(int))]
        public async Task<IFlipflopContext> WaitTask(CommandSignal command = CommandSignal.Command_1)
        {
            try
            {
                TriggerData triggerData = await ViewManagement.CreateChannelWithTimeoutAsync(command, TimeSpan.FromMinutes(120), 0);
                if (triggerData.Type == TriggerType.Overtime)
                {
                    throw new FlipflopException("超时取消");
                }
                return new FlipflopContext(FlipflopStateType.Succeed, triggerData.Value);
            }
            catch (FlipflopException)
            {
                throw;
            }
            catch (Exception)
            {
                return new FlipflopContext(FlipflopStateType.Error);
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
