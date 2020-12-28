using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Handler
{
    public class RetryHandler : DelegatingHandler
    {
        private readonly int _maxRetries = 3;

        public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        public RetryHandler(HttpMessageHandler innerHandler, int maxRetries) : base(innerHandler)
        {
            _maxRetries = maxRetries;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            for (var i = 0; i < _maxRetries; i++)
            {
                try
                {
                    response = await base.SendAsync(request, cancellationToken);
                }
                catch (Exception e)
                {
                    Trace.WriteLine(e);
                    continue;
                }

                if (response.IsSuccessStatusCode) return response;
            }

            return response;
        }
    }
}