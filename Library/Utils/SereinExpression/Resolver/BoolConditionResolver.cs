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

        public override bool Evaluate(object obj)
        {

            if (obj is bool boolObj)
            {
                return boolObj == Value;
                /*switch (Op)
                {
                    case Operator.Is:
                        return boolObj == Value;
                }*/
            }
            return false;
        }
    }

}
