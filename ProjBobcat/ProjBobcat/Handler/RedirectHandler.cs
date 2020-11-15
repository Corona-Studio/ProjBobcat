using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Handler
{
    public class RedirectHandler : DelegatingHandler
    {
        private readonly int _maxRetries = 20;
        private int _currentRetries;

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
            _currentRetries++;
            var redirectUri = response.Headers.Location;
            if (!redirectUri.IsAbsoluteUri)
            {
                redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);
            }

            Console.WriteLine("重定向至：" + redirectUri);

            using var newRequest = new HttpRequestMessage(request.Method, redirectUri);
            return await base.SendAsync(newRequest, cancellationToken);
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            var statusCode = (int)response.StatusCode;

            return statusCode >= 300 && statusCode <= 399
                ? _currentRetries == _maxRetries ? response :
                await CreateRedirectResponse(request, response, cancellationToken)
                : response;
        }
    }
}