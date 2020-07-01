using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    /// Http工具方法类
    /// </summary>
    public static class HttpHelper
    {
        /// <summary>
        /// 正则匹配Uri
        /// </summary>
        /// <param name="uri">待处理Uri</param>
        /// <returns>匹配的Uri</returns>
        public static string RegexMatchUri(string uri)
        {
            var r = new Regex(
                "((([A-Za-z]{3,9}:(?:\\/\\/)?)(?:[-;:&=\\+$,\\w]+@)?[A-Za-z0-9.-]+(:[0-9]+)?|(?:ww‌​w.|[-;:&=\\+$,\\w]+@)[A-Za-z0-9.-]+)((?:\\/[\\+~%\\/.\\w-_]*)?\\??(?:[-\\+=&;%@.\\w_]*)#?‌​(?:[\\w]*))?)");
            return r.Match(uri).Value;
        }

        /// <summary>
        /// Http Get方法
        /// </summary>
        /// <param name="address">Get地址</param>
        /// <returns>获取到的字符串</returns>
        public static async Task<string> Get(string address)
        {
            using var client = new HttpClient();
            return await client.GetStringAsync(new Uri(address)).ConfigureAwait(true);
        }

        /// <summary>
        /// Http Post方法
        /// </summary>
        /// <param name="address">Post地址</param>
        /// <param name="data">数据</param>
        /// <param name="contentType">ContentType</param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> Post(string address, string data,
            string contentType = "application/json")
        {
            using var client = new HttpClient();
            using var content = new StringContent(data);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            var response = await client.PostAsync(new Uri(address), content).ConfigureAwait(true);
            return response;
        }

        /// <summary>
        /// Http Post方法（带参数）
        /// </summary>
        /// <param name="address">Post地址</param>
        /// <param name="param">参数</param>
        /// <param name="contentType">ContentType</param>
        /// <returns></returns>
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