using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operation
{

    class Test {

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
          undoStack.Push(operation); 
          redoStack.Clear();
         */


        /// <summary>
        /// 撤销
        /// </summary>
        public void Undo()
        {
            if (undoStack.Count > 0)
            {
                var command = undoStack.Pop();
                command.Undo();  // 执行撤销
                redoStack.Push(command);  // 将撤销的命令推入重做栈
            }
        }

        /// <summary>
        /// 重做
        /// </summary>
        public void Redo()
        {
            if (redoStack.Count > 0)
            {
                var command = redoStack.Pop();
                command.Execute(); 
                undoStack.Push(command);  // 将重做的命令推入撤销栈
            }
        }

    }


    internal class OperationInfo
    {

    }

    internal abstract class OperationBase : IOperation
    {
        /// <summary>
        /// 操作的主题
        /// </summary>
        public required string Theme { get; set; }

        public abstract void Execute();
        public abstract void Undo();
        public abstract void ToInfo();

        protected OperationBase()
        {
            
        }
        protected OperationBase(OperationInfo info)
        {
            
        }

    }

    internal interface IOperation
    {
        void Execute();  // 执行操作
        void Undo();     // 撤销操作

        
    }
}
