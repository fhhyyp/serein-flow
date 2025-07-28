using Serein.Library;
using System.Reflection;

namespace Serein.NodeFlow.Model.Library
{
    public class LibraryMdDd
    {
        public MethodDetails MethodDetails { get;  }
        public MethodInfo MethodInfo { get;  }
        public DelegateDetails DelegateDetails { get; }

        public LibraryMdDd(MethodInfo methodInfo, MethodDetails methodDetails, DelegateDetails delegateDetails)
        {
            MethodDetails = methodDetails;
            MethodInfo = methodInfo;
            DelegateDetails = delegateDetails;
        }
    }

}
