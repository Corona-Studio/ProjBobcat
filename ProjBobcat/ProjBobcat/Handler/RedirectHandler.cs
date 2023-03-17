using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Handler;

/// <summary>
///     HttpClient 重定向助手
/// </summary>
public class RedirectHandler : DelegatingHandler
{
    readonly int _maxRetries = 20;

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

    async Task<HttpResponseMessage?> CreateRedirectResponse(HttpRequestMessage request,
        HttpResponseMessage? response, CancellationToken cancellationToken)
    {
        var redirectUri = response?.Headers?.Location;

        if (redirectUri == null) return null;
        if (request.RequestUri == null) return null;

        if (!redirectUri.IsAbsoluteUri)
            redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);

        using var newRequest = new HttpRequestMessage(request.Method, redirectUri);
        newRequest.Headers.Host = request.Headers.Host;

        return await base.SendAsync(newRequest, cancellationToken);
    }

    protected override async Task<HttpResponseMessage?> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var currentRedirect = 0;
        var response = await base.SendAsync(request, cancellationToken);
        var statusCode = response?.StatusCode;

        while (currentRedirect < _maxRetries &&
               statusCode is
                   HttpStatusCode.MovedPermanently or
                   HttpStatusCode.Found or
                   HttpStatusCode.PermanentRedirect)
        {
            Debug.WriteLine($"第{currentRedirect}次重定向");
            response = await CreateRedirectResponse(request, response, cancellationToken);
            statusCode = response?.StatusCode;
            currentRedirect++;
        }

        return response;
    }
}