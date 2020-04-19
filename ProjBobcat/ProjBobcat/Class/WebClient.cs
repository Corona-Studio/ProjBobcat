using System;
using System.Net;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Class
{
    public class WebClient : System.Net.WebClient
    {
        public WebClient()
        {
            Timeout = 10 * 1000;
        }

        public DownloadRange DownloadRange { get; set; }
        public int Timeout { get; set; }

        protected override WebRequest GetWebRequest(Uri uri)
        {
            var lWebRequest = base.GetWebRequest(uri);
            lWebRequest.Timeout = Timeout;

            if (!(lWebRequest is HttpWebRequest webRequest)) return lWebRequest;
            webRequest.ReadWriteTimeout = Timeout;

            if (DownloadRange != null && !DownloadRange.Equals(default(DownloadRange)))
                webRequest.AddRange(DownloadRange.Start, DownloadRange.End);

            return lWebRequest;
        }
    }
}