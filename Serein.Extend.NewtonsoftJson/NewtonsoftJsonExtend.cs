using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Extend.NewtonsoftJson
{
    public static class NewtonsoftJsonExtend
    {
        public static string ToJsonText(this object data)
        {
            return JsonHelper.Serialize(data);
        }
    }
}
