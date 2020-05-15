using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RestSharp;

namespace ProjBobcat.Class.Helper
{
    public static class HttpHelper
    {
        public static string RegexMatchUri(string uri)
        {
            var r = new Regex(
                "((([A-Za-z]{3,9}:(?:\\/\\/)?)(?:[-;:&=\\+$,\\w]+@)?[A-Za-z0-9.-]+(:[0-9]+)?|(?:ww‌​w.|[-;:&=\\+$,\\w]+@)[A-Za-z0-9.-]+)((?:\\/[\\+~%\\/.\\w-_]*)?\\??(?:[-\\+=&;%@.\\w_]*)#?‌​(?:[\\w]*))?)");
            return r.Match(uri).Value;
        }

        public static async Task<string> Get(string address)
        {
            using var client = new HttpClient();
            return await client.GetStringAsync(new Uri(address)).ConfigureAwait(true);
        }

        public static async Task<IRestResponse> Post(string address, string data,
            string contentType = "application/json")
        {
            //using var client = new HttpClient();
            using var content = new StringContent(data);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            /*HttpWebRequest httpWebRequest = WebRequest.Create(address) as HttpWebRequest;
            httpWebRequest.Method = "POST";
            httpWebRequest.ContentType = contentType;//application/json
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            using (Stream stream = httpWebRequest.GetRequestStream())
            {
                stream.Write(dataBytes, 0, dataBytes.Length);
            }
            WebResponse webResponse = httpWebRequest.GetResponse() as HttpWebResponse;
            Stream dataStream = webResponse.GetResponseStream();
            StreamReader reader = new StreamReader(dataStream, Encoding.UTF8);
            
            string returnStr = reader.ReadToEnd();
            reader.Close();
            webResponse.Close();
            */
            var client = new RestClient(address);
            var request = new RestRequest(Method.POST);

            request.AddHeader("cache-control", "no-cache");
            request.AddHeader("content-type", "application/json");
            request.AddParameter(contentType, data, ParameterType.RequestBody);
            IRestResponse response = client.Execute(request);
            //client.Timeout=new TimeSpan(0,0,10);
            //var response =await client.PostAsync(new Uri(address), content).ConfigureAwait(false);
            
            return response;
        }

        public static async Task<HttpResponseMessage> PostWithParams(string address,
            IEnumerable<KeyValuePair<string, string>> param, string contentType = "application/json")
        {
            using var client = new HttpClient();
            using var content = new FormUrlEncodedContent(param);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            var response = await client.PostAsync(new Uri(address), content).ConfigureAwait(true);
            return response;
        }
    }
}