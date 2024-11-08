using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Utils
{
    public static class SereinEnv
    {
        private static IFlowEnvironment environment;
        public static void SetEnv(IFlowEnvironment environment)
        {
            if (environment != null)
            {
                SereinEnv.environment = environment;
            }
        }
        public static void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial)
        {
            SereinEnv.environment.WriteLine(type,message,@class);
        }
    }

}
