using System;
using System.Net;

namespace ProjBobcat.Class
{
    public class WebClient : System.Net.WebClient
    {
        public WebClient()
        {
            Timeout = 10 * 1000;
        }

        public int Timeout { get; set; }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            var lWebRequest = base.GetWebRequest(uri);
            lWebRequest.Timeout = Timeout;
            if (lWebRequest is HttpWebRequest webRequest) webRequest.ReadWriteTimeout = Timeout;
            return lWebRequest;
        }
    }
}