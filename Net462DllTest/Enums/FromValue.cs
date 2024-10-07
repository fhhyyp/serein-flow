using Net462DllTest.View;
using Serein.Library.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Signal
{
    public enum FromValue
    {
        [BindValue(typeof(FromWorkBenchView))]
        FromWorkBenchView,
        [BindValue(typeof(TestFormView))]
        TestFormView,
    }
}
