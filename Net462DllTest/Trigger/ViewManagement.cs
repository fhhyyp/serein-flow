using Net462DllTest.Signal;
using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Threading;
using Serein.Library.Utils;
using Serein.Library.Utils.FlowTrigger;

namespace Net462DllTest.Trigger
{
    /// <summary>
    /// 视图管理
    /// </summary>
    [AutoRegister]
    public class ViewManagement : TaskFlowTrigger<CommandSignal>
    {
        private readonly UIContextOperation uiContextOperation;
        public ViewManagement(UIContextOperation uiContextOperation)
        {
            this.uiContextOperation = uiContextOperation;
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
            //Application.Current.Dispatcher.
            forms.Add(form);

            uiContextOperation.Invoke(() => {
                form.TopMost = isTop;
                form.Show();
            });



            //environment.IOC.Run<SynchronizationContext>(uiContext =>
            //{
            //    uiContext?.Post(state => {
                    
            //    },null);
            //});

            //var uiContext = SynchronizationContext.Current;
            //Task.Run(() =>
            //{
            //    uiContext.Post(_ =>
            //    {
                  
            //    }, null);
            //});

        }



        public void CloseView(Type formType)
        {
            var remoteForms = forms.Where(f => f.GetType() == formType).ToArray();

            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                foreach (Form f in remoteForms)
                {
                    f.Close();
                    f.Dispose();
                    this.forms.Remove(f);
                }
            });
            
        }


    }

}
