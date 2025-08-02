using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 数值型节点
    /// </summary>
    public abstract class NumberNode<T> : ValueNode<T> where T : struct, IComparable<T>
    {
        public NumberNode(T value) => Value = value;
    }


    /// <summary>
    /// int 整数型字面量
    /// </summary>
    public class NumberIntNode(int vlaue) : NumberNode<int>(vlaue)
    {
    }

    /// <summary>
    /// long 整数型字面量
    /// </summary>
    public class NumberLongNode(long vlaue) : NumberNode<long>(vlaue)
    {
    }

    /// <summary>
    /// float 字面量
    /// </summary>
    public class NumberFloatNode(float vlaue) : NumberNode<float>(vlaue)
    {
    }

    /// <summary>
    /// double 字面量
    /// </summary>
    public class NumberDoubleNode(double vlaue) : NumberNode<double>(vlaue)
    {
    }




    /*/// <summary>
    /// int 整数型字面量
    /// </summary>
    public class NumberIntNode : ASTNode
    {
        public int Value { get; }
        public NumberIntNode(int value) => Value = value;
    }
    

    /// <summary>
    /// int 整数型字面量
    /// </summary>
    public class NumberLongNode : ASTNode
    {
        public long Value { get; }
        public NumberLongNode(long value) => Value = value;
    }
    

    /// <summary>
    /// int 整数型字面量
    /// </summary>
    public class NumberFloatNode : ASTNode
    {
        public float Value { get; }
        public NumberFloatNode(float value) => Value = value;
    }
    

    /// <summary>
    /// int 整数型字面量
    /// </summary>
    public class NumberDoubleNode : ASTNode
    {
        public double Value { get; }
        public NumberDoubleNode(double value) => Value = value;
    }*/


}
