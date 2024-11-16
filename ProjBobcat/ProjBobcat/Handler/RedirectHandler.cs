using System;
using System.Diagnostics;
using System.IO;
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

    public RedirectHandler(HttpMessageHandler innerHandler) : base(innerHandler)
    {
    }

    public RedirectHandler(HttpMessageHandler innerHandler, int maxRetries) : base(innerHandler)
    {
        this._maxRetries = maxRetries;
    }

    static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(
        HttpRequestMessage req,
        Uri? reqUri = null)
    {
        var clone = new HttpRequestMessage(req.Method, reqUri ?? req.RequestUri);

        if (req.Content != null)
        {
            var ms = new MemoryStream();

            await req.Content.CopyToAsync(ms).ConfigureAwait(false);

            ms.Seek(0, SeekOrigin.Begin);
            clone.Content = new StreamContent(ms);

            foreach (var h in req.Content.Headers)
                clone.Content.Headers.Add(h.Key, h.Value);
        }

        clone.Version = req.Version;

        foreach (var option in req.Options)
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);

        foreach (var header in req.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }

    async Task<HttpResponseMessage?> CreateRedirectResponse(
        HttpRequestMessage request,
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var redirectUri = response.Headers.Location;

        Debug.Assert(redirectUri is not null, "RedirectUri cannot be null!");
        Debug.Assert(request.RequestUri is not null, "RequestUri cannot be null!");

        if (!redirectUri.IsAbsoluteUri)
            redirectUri = new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority) + redirectUri);

        using var newRequest = await CloneHttpRequestMessageAsync(request, redirectUri);

        return await base.SendAsync(newRequest, cancellationToken);
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var currentRedirect = 0;
        var response = await base.SendAsync(request, cancellationToken);
        var statusCode = response.StatusCode;

        while (currentRedirect < this._maxRetries &&
               statusCode is
                   HttpStatusCode.MovedPermanently or
                   HttpStatusCode.Found or
                   HttpStatusCode.PermanentRedirect)
        {
            Debug.WriteLine($"第{currentRedirect}次重定向");

            var redirectedRes = await this.CreateRedirectResponse(request, response, cancellationToken);

            try
            {
                request.Dispose();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception)
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest);
            }

            if (redirectedRes == null) return new HttpResponseMessage(HttpStatusCode.BadRequest);

            response = redirectedRes;
            statusCode = response.StatusCode;
            currentRedirect++;
        }

        return response;
    }
}