using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper
{
    public static class HttpHelper
    {
        public static async Task<string> Get(string address)
        {
            using var client = new HttpClient();
            return await client.GetStringAsync(new Uri(address)).ConfigureAwait(true);
        }

        public static async Task<HttpResponseMessage> Post(string address, string data, string contentType = "application/json")
        {
            using var client = new HttpClient();
            using var content = new StringContent(data);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            var response = await client.PostAsync(new Uri(address), content).ConfigureAwait(true);
            return response;
        }

        public static async Task<HttpResponseMessage> PostWithParams(string address, IEnumerable<KeyValuePair<string, string>> param, string contentType = "application/json")
        {
            using var client = new HttpClient();
            using var content = new FormUrlEncodedContent(param);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            var response = await client.PostAsync(new Uri(address), content).ConfigureAwait(true);
            return response;
        }
    }
}