namespace Serein.Library.Http
{
    public class ControllerBase
    {

        public string Url { get; set; }


        public string BobyData { get; set; }


        public string GetLog(Exception ex)
        {
            return "Url : " + Url + Environment.NewLine +
                   "Ex : " + ex.Message + Environment.NewLine +
                   "Data : " + BobyData + Environment.NewLine;
        }
    }
}
