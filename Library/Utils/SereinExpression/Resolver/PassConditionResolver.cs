using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils.SereinExpression.Resolver
{
    public class PassConditionResolver : SereinConditionResolver
    {
        public Operator Op { get; set; }
        public override bool Evaluate(object obj)
        {
            /*return Op switch
            {
                Operator.Pass => true,
                Operator.NotPass => false,
                _ => throw new NotSupportedException("不支持的条件类型")
            };*/
            switch (Op)
            {
                case Operator.Pass:
                    return true;
                case Operator.NotPass:
                    return false;
                default:
                    throw new NotSupportedException("不支持的条件类型");

            }
        }

        public enum Operator
        {
            Pass,
            NotPass,
        }
    }
}
