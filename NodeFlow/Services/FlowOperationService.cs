using Serein.Library.Api;
using Serein.NodeFlow.Model.Operations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Services
{
    /// <summary>
    /// 流程操作
    /// </summary>
    internal class FlowOperationService 
    {
        private readonly ISereinIOC sereinIOC;

        public FlowOperationService(ISereinIOC sereinIOC)
        {
            this.sereinIOC = sereinIOC;
        }

        /// <summary>
        /// 撤销栈
        /// </summary>
        private Stack<IOperation> undoStack = [];
        /// <summary>
        /// 重做栈
        /// </summary>
        private Stack<IOperation> redoStack = [];


        /*
          // 执行新命令时，将命令推入撤销栈，并清空重做栈
         */
        /// <summary>
        /// 撤销
        /// </summary>
        public void Undo()
        {
            if (undoStack.Count > 0)
            {
                var command = undoStack.Pop();
                var state = command.Undo();  // 执行撤销
                if (state)
                {
                    redoStack.Push(command);  // 将撤销的命令推入重做栈

                }
            }
        }

        /// <summary>
        /// 重做
        /// </summary>
        public async Task Redo()
        {
            if (redoStack.Count > 0)
            {
                var command = redoStack.Pop();
                var state =  await command.ExecuteAsync();
                if (state)
                {
                    undoStack.Push(command);  // 将重做的命令推入撤销栈
                }
            }
        }


        internal async Task Execute(IOperation operation)
        {
            sereinIOC.InjectDependenciesProperty(operation); // 注入所需要的依赖
            var state =  await operation.ExecuteAsync();
            if (state)
            {
                // 执行后，推入撤销栈，并清空重做栈
                undoStack.Push(operation);
                redoStack.Clear();
            }
          
        }

    }
}
