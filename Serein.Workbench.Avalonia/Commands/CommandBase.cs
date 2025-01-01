using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Commands
{
    internal abstract class CommandBase
    {
        // CanExecuteChanged 事件
        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// 是否可以执行命令，子类可以重写这个方法来提供具体的可执行条件
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public virtual bool CanExecute(object parameter)
        {
            return true;  // 默认实现返回 true，表示命令可以执行
        }

        /// <summary>
        /// 执行命令，子类可以重写这个方法来实现具体的命令逻辑
        /// </summary>
        /// <param name="parameter"></param>
        public abstract void Execute(object parameter);

        /// <summary>
        /// 用于触发 CanExecuteChanged 事件
        /// </summary>
        protected void OnCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
