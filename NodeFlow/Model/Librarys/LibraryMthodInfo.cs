using Serein.Library;
using System.Reflection;

namespace Serein.NodeFlow.Model.Library
{
    /// <summary>
    /// 库方法信息类，包含方法的详细信息、反射信息和委托信息。
    /// </summary>
    public class LibraryMthodInfo
    {
        /// <summary>
        /// 方法的详细信息。
        /// </summary>
        public MethodDetails MethodDetails { get;  }

        /// <summary>
        /// 方法的反射信息，包含方法名称、参数类型等。
        /// </summary>
        public MethodInfo MethodInfo { get;  }
        /// <summary>
        /// Emit构造委托
        /// </summary>
        public DelegateDetails DelegateDetails { get; }

        /// <summary>
        /// 构造一个新的库方法信息实例。
        /// </summary>
        /// <param name="methodInfo"></param>
        /// <param name="methodDetails"></param>
        /// <param name="delegateDetails"></param>
        public LibraryMthodInfo(MethodInfo methodInfo, MethodDetails methodDetails, DelegateDetails delegateDetails)
        {
            MethodDetails = methodDetails;
            MethodInfo = methodInfo;
            DelegateDetails = delegateDetails;
        }
    }

}
