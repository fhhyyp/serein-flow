using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.Http
{
    internal static partial class SereinExtension
    {
        #region JSON相关

        public static bool ToBool(this JToken token, bool defult = false)
        {
            var value = token?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defult;
            }
            if (!bool.TryParse(value, out bool result))
            {
                return defult;
            }
            else
            {
                return result;
            }
        }
        public static int ToInt(this JToken token, int defult = 0)
        {
            var value = token?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defult;
            }
            if (!int.TryParse(value, out int result))
            {
                return defult;
            }
            else
            {
                return result;
            }
        }
        public static double ToDouble(this JToken token, double defult = 0)
        {
            var value = token?.ToString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return defult;
            }
            if (!int.TryParse(value, out int result))
            {
                return defult;
            }
            else
            {
                return result;
            }
        } 
        #endregion
    }
}
