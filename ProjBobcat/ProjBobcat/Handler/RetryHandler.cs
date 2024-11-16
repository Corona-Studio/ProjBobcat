using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Handler;

public class RetryHandler : DelegatingHandler
{
    readonly int _maxRetries = 5;

    public RetryHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    public RetryHandler(HttpMessageHandler innerHandler, int maxRetries) : base(innerHandler)
    {
        this._maxRetries = maxRetries;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage? response = null;

        for (var i = 0; i < this._maxRetries; i++)
        {
            try
            {
                response = await base.SendAsync(request, cancellationToken);
            }
            catch (Exception e)
            {
                if (IsNetworkError(e))
                    continue;

                throw;
            }

            return response;
        }

        return response ?? new HttpResponseMessage(HttpStatusCode.BadRequest);
    }

    static bool IsNetworkError(Exception ex)
    {
        while (true)
        {
            if (ex is SocketException) return true;
            if (ex.InnerException == null) return false;

            ex = ex.InnerException;
        }
    }
}