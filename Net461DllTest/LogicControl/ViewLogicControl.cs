using Net461DllTest.Signal;
using Net461DllTest.ViewModel;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Net461DllTest.LogicControl
{

    public class ViewManagement
    {

        private List<Form> forms = new List<Form>();
        public void OpenView(Form form, bool isTop)
        {
            form.FormClosing += (s, e) =>
            {
                // 关闭窗体时执行一些关于逻辑层的操作
            };
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


   

    [DynamicFlow]
    public class ViewLogicControl
    {
        [AutoInjection]
        public ViewManagement ViewManagement { get; set; }

        [NodeAction(NodeType.Init)] 
        public void Init(IDynamicContext context)
        {
            context.Env.IOC.Register<ViewManagement>();
            context.Env.IOC.Register<FromWorkBenchViewModel>();
        }


        [NodeAction(NodeType.Action, "打开窗体（指定枚举值）")]
        public void OpenForm(IDynamicContext context, FromValue fromId = FromValue.None, bool isTop = true)
        {
            var fromType = EnumHelper.GetBoundValue<FromValue, Type>(fromId, attr => attr.Value);
            if (fromType is null) return;
            if (context.Env.IOC.Instantiate(fromType) is Form form)
            {
                ViewManagement.OpenView(form, isTop);
            }
        }

        [NodeAction(NodeType.Action, "打开窗体（使用转换器）")]
        public void OpenForm2([EnumTypeConvertor(typeof(FromValue))] Form form, bool isTop = true)
        {
            ViewManagement.OpenView(form, isTop);
        }



        [NodeAction(NodeType.Action, "关闭窗体")]
        public void CloseForm(IDynamicContext context, FromValue fromId = FromValue.None)
        {
            var fromType = EnumHelper.GetBoundValue<FromValue, Type>(fromId, attr => attr.Value);
            if (fromType is null) return;
            ViewManagement.CloseView(fromType);
        }





    }
}
