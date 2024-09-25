using System;
using System.Collections.Generic;
using System.Text;

namespace Serein.Library.Entity
{
    // 每次发生调用的时候，将当前节点调用信息拷贝一份，
    // 调用完成后释放？
    // 参数信息
    public class CallChainInfo
    {
        public List<string> CallGuid { get; }
        public List<object[]> InvokeData { get; }
        public List<object> ResultData { get; }
    }

    
}
