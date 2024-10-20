using Net462DllTest.View;
using Serein.Library;

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
