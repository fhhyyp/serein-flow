using Net462DllTest.Signal;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.NodeFlow.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Net462DllTest.Trigger
{
    /// <summary>
    /// 视图管理
    /// </summary>
    [AutoRegister]
    public class ViewManagement : FlowTrigger<CommandSignal>
    {
        public ViewManagement(IFlowEnvironment environment)
        {
            
        }
        public int Id = new Random().Next(1, 10000);
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
            var remoteForms = forms.Where(f => f.GetType() == formType).ToArray();
            foreach (Form f in remoteForms)
            {
                f.Close();
                f.Dispose();
                this.forms.Remove(f);
            }
        }
    }

}
