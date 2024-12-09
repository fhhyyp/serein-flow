using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model
{
    /// <summary>
    /// 全局数据节点
    /// </summary>
    public class SingleGlobalDataNode : NodeModelBase
    {
        public SingleGlobalDataNode(IFlowEnvironment environment) : base(environment)
        {
        }

        public override ParameterData[] GetParameterdatas()
        {
            throw new NotImplementedException();
        }

        public override void OnCreating()
        {
            throw new NotImplementedException();
        }
    }
}
