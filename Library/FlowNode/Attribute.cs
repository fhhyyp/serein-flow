using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    [AttributeUsage(AttributeTargets.Class, Inherited = true)]
    internal sealed class AutoPropertyAttribute : Attribute
    {
        public string ValuePath = string.Empty;
    }

    /// <summary>
    /// 自动生成环境的属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    internal sealed class PropertyInfoAttribute : Attribute
    {
        public bool IsNotification = false;
        public bool IsPrint = false;
    }

}
