using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     Http工具方法类
    /// </summary>
    public static class HttpHelper
    {
        private static readonly HttpClient Client = HttpClientHelper.GetClient();

        /// <summary>
        ///     正则匹配Uri
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
        ///     Http Get方法
        /// </summary>
        /// <param name="address">Get地址</param>
        /// <param name="auth">Auth 字段</param>
        /// <returns>获取到的字符串</returns>
        public static async Task<string> Get(string address, Tuple<string, string> auth = default)
        {
            var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);
            Client.DefaultRequestHeaders.AcceptLanguage.Add(acceptLanguage);

            if (!(auth?.Equals(default) ?? true))
            {
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(auth.Item1, auth.Item2);
            }

            return await Client.GetStringAsync(new Uri(address));
        }

        /// <summary>
        /// Http Post Form 数据
        /// </summary>
        /// <param name="address">Post 地址</param>
        /// <param name="data">数据</param>
        /// <param name="contentType">类型</param>
        /// <returns>服务器返回数据</returns>
        public static async Task<HttpResponseMessage> PostFormData(string address,
            IEnumerable<KeyValuePair<string, string>> data, string contentType = "application/json")
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, address)
            {
                Content = new FormUrlEncodedContent(data)
            };
            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

            var response = await Client.SendAsync(req);
            return response;
        }

        /// <summary>
        ///     Http Post方法
        /// </summary>
        /// <param name="address">Post地址</param>
        /// <param name="data">数据</param>
        /// <param name="contentType">ContentType</param>
        /// <param name="auth">Auth 字段</param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> Post(string address, string data,
            string contentType = "application/json", Tuple<string, string> auth = default)
        {
            using var content = new StringContent(data);

            Client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

            if (!(auth?.Equals(default) ?? true))
            {
                Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(auth.Item1, auth.Item2);
            }

            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);

            Client.DefaultRequestHeaders.AcceptLanguage.Add(acceptLanguage);
            var response = await Client.PostAsync(new Uri(address), content);
            return response;
        }

        /// <summary>
        ///     Http Post方法（带参数）
        /// </summary>
        /// <param name="address">Post地址</param>
        /// <param name="param">参数</param>
        /// <param name="contentType">ContentType</param>
        /// <returns></returns>
        public static async Task<HttpResponseMessage> PostWithParams(string address,
            IEnumerable<KeyValuePair<string, string>> param, string contentType = "application/json")
        {
            using var content = new FormUrlEncodedContent(param);
            content.Headers.ContentType = new MediaTypeWithQualityHeaderValue(contentType);
            var acceptLanguage = new StringWithQualityHeaderValue(CultureInfo.CurrentCulture.Name);
            Client.DefaultRequestHeaders.AcceptLanguage.Add(acceptLanguage);
            var response = await Client.PostAsync(new Uri(address), content);
            return response;
        }
    }
}