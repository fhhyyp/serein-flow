using Net461DllTest.Data;
using Net461DllTest.Device;
using Net461DllTest.View;
using Net461DllTest.ViewModel;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace Net461DllTest.Flow
{

    public class ViewManagement
    {

        private List<Form> forms = new List<Form>();
        public void OpenView(Form form)
        {
            form.FormClosing += (s, e) =>
            {
                // 关闭窗体时执行一些关于逻辑层的操作
            };
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


    public enum FromId
    {
        None,
        [BindValue(typeof(FromWorkBenchView))]
        FromWorkBenchView,
        [BindValue(typeof(TeseFormView))]
        TeseFormView,
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
        public void OpenForm(IDynamicContext context, FromId fromId = FromId.None)
        {
            var fromType = EnumHelper.GetBoundValue<FromId, Type>(fromId, attr => attr.Value);
            if (fromType is null) return;
            if (context.Env.IOC.Instantiate(fromType) is Form form)
            {
                ViewManagement.OpenView(form);
                
            }
        }

        [NodeAction(NodeType.Action, "打开窗体（使用转换器）")]
        public void OpenForm2([EnumTypeConvertor(typeof(FromId))] Form form)
        {
            ViewManagement.OpenView(form);
        }


        [NodeAction(NodeType.Action, "关闭窗体")]
        public void CloseForm(IDynamicContext context, FromId fromId = FromId.None)
        {
            var fromType = EnumHelper.GetBoundValue<FromId, Type>(fromId, attr => attr.Value);
            if (fromType is null) return;
            ViewManagement.CloseView(fromType);
        }




    }
}
