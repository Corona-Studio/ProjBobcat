using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Handler
{
    /// <summary>
    ///     HttpClient 重定向助手
    /// </summary>
    public class RedirectHandler : DelegatingHandler
    {
        private readonly int _maxRetries = 20;

        public RedirectHandler()
        {
        }

        public RedirectHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        public RedirectHandler(HttpMessageHandler innerHandler, int maxRetries) : base(innerHandler)
        {
            _maxRetries = maxRetries;
        }

        private async Task<HttpResponseMessage> CreateRedirectResponse(HttpRequestMessage request,
            HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var redirectUri = response.Headers.Location;
            if (!redirectUri.IsAbsoluteUri)
                redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);

            using var newRequest = new HttpRequestMessage(request.Method, redirectUri);
            newRequest.Headers.Host = request.Headers.Host;

            return await base.SendAsync(newRequest, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var statusCode = (int) response?.StatusCode;

            return statusCode switch
            {
                0 => null,
                < 300 or > 399 => response,
                _ => await CreateRedirectResponse(request, response, cancellationToken)
            };
        }
    }
}