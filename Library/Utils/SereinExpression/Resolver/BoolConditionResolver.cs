using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils.SereinExpression.Resolver
{
    public class BoolConditionResolver : SereinConditionResolver
    {
        public enum Operator
        {
            /// <summary>
            /// 是
            /// </summary>
            Is
        }

        public Operator Op { get; set; }
        public bool Value { get; set; }
        public bool Data { get; set; }

        public override bool Evaluate(object obj)
        {

            return Value.Equals(Data);
            //if (obj is bool boolObj && Value is bool boolValue)
            //{

            //    /*switch (Op)
            //    {
            //        case Operator.Is:
            //            return boolObj == Value;
            //    }*/
            //}
            //return false;
        }
    }

}
