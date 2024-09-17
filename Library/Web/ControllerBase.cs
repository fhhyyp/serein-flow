using System;

namespace Serein.Library.Web
{
    public class ControllerBase
    {
        // [AutoInjection]
        // public ILoggerService loggerService { get; set; }
        public string Url { get; set; }
        public string BobyData { get; set; }

        public string GetLog(Exception ex)
        {
            return "Url : " + Url + Environment.NewLine +
                   "Ex : " + ex + Environment.NewLine +
                   "Data : " + BobyData + Environment.NewLine;
        }
    }
}
