using Net461DllTest.View;
using Serein.Library.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net461DllTest.Signal
{
    public enum FromValue
    {
        None,
        [BindValue(typeof(FromWorkBenchView))]
        FromWorkBenchView,
        [BindValue(typeof(TestFormView))]
        TestFormView,
    }
}
