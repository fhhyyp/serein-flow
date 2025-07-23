using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public class TriggerResult<TResult>
    {
        public TriggerDescription Type { get; set; }
        public TResult Value { get; set; }
    }
}
