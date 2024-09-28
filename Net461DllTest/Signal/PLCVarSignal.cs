using Serein.Library.Attributes;
using System;
using static Net461DllTest.Signal.PlcValueAttribute;

namespace Net461DllTest.Signal
{

    [AttributeUsage(AttributeTargets.Field)]
    public class PlcValueAttribute : Attribute
    {
        /// <summary>
        /// 变量类型
        /// </summary>
        public enum VarType
        {
            /// <summary>
            /// 只读取的值
            /// </summary>
            ReadOnly,
            /// <summary>
            /// 可写入的值
            /// </summary>
            Writable,
        }
        
        /// <summary>
        /// 变量属性
        /// </summary>
        public PlcVarInfo PlcInfo { get; }


        public PlcValueAttribute(Type type,
                                string @var,
                                VarType varType
                                )
        {
            PlcInfo = new PlcVarInfo(type, var, varType);
        }
    }

    public class PlcVarInfo
    {
        public PlcVarInfo(Type type,
                        string @var,
                        VarType varType
                        )
        {
            DataType = type;
            VarAddress = @var;
            Type = varType;
        }
        public bool IsProtected { get; }
        public Type DataType { get; }
        public string VarAddress { get; }
        public VarType Type { get; }

        public override string ToString()
        {
            return $"数据类型:{DataType} 地址:{VarAddress}";
        }
    }


}
