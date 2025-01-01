using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Commands
{
    /// <summary>
    /// 流程控制命令
    /// </summary>
    internal class MyCommand : CommandBase
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        /// <summary>
        /// 构造函数接收执行动作和是否可执行的条件
        /// </summary>
        /// <param name="execute"></param>
        /// <param name="canExecute"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public MyCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 重写 CanExecute 方法，基于 _canExecute 委托的结果来判断命令是否可执行
        /// </summary>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public override bool CanExecute(object parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        /// <summary>
        /// 重写 Execute 方法，执行具体的命令逻辑
        /// </summary>
        /// <param name="parameter"></param>
        public override void Execute(object parameter)
        {
            _execute();
        }
    }
}
