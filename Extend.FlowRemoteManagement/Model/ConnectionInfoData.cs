using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.FlowRemoteManagement.Model
{
    public class ConnectionInfoData
    {
        public bool Op { get; set; }
        public string? FromNodeGuid { get; set; }
        public string? ToNodeGuid { get; set; }
        // None  Upstream   IsSucceed   IsFail IsError
        public string? Type { get; set; }
    }
}
