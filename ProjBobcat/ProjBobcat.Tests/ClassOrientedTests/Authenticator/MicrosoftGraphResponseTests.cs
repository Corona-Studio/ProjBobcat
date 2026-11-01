using System.Net;
using System.Text.Json;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Microsoft.Graph;
using ProjBobcat.Class.Model.MicrosoftAuth;
using ProjBobcat.DefaultComponent.Authenticator;

namespace ProjBobcat.Tests.ClassOrientedTests.Authenticator;

[TestClass]
[DoNotParallelize]
public class MicrosoftGraphResponseTests
{
    private const string TokenJson = """
        {"access_token":"access","refresh_token":"refresh","expires_in":3600}
        """;

    // Poll immediately: the server's interval is increased by two seconds by the authenticator.
    private const string DeviceJson = """
        {"user_code":"user","device_code":"device","verification_uri":"https://example.test/device","interval":-2}
        """;

    [TestMethod]
    public void ParsesTokenWithoutLosingTheRequestedType()
    {
        var result = MicrosoftAuthenticator.ResolveMSGraphResult(TokenJson,
            SerializerContext.Default.GraphAuthResultModel);

        var token = result switch
        {
            GraphAuthResultModel success => success,
            GraphResponseErrorModel or null => null
        };
        Assert.IsNotNull(token);
        Assert.AreEqual("access", token.AccessToken);
        Assert.AreEqual("refresh", token.RefreshToken);
        Assert.AreEqual(3600, token.ExpiresIn);
    }

    [TestMethod]
    public void ParsesOAuthErrorWithItsTimestamp()
    {
        var result = MicrosoftAuthenticator.ResolveMSGraphResult("""
            {"error":"authorization_declined","error_description":"Declined","timestamp":"2026-11-01 10:20:30Z"}
            """, SerializerContext.Default.GraphAuthResultModel);

        Assert.IsTrue(result is GraphResponseErrorModel);
        var error = (GraphResponseErrorModel)result.Value!;
        Assert.AreEqual("authorization_declined", error.ErrorType);
        Assert.AreEqual("Declined", error.ErrorDescription);
        Assert.AreEqual(2026, error.TimeStamp.Year);
    }

    [TestMethod]
    public void JsonNullAndDefaultUnionRepresentNoPayload()
    {
        var parsed = MicrosoftAuthenticator.ResolveMSGraphResult("null",
            SerializerContext.Default.GraphAuthResultModel);
        GraphResponse<GraphAuthResultModel> empty = default;

        Assert.IsTrue(parsed is null);
        Assert.IsTrue(empty is null);
    }

    [TestMethod]
    public void InvalidPayloadStillReportsTheJsonError()
    {
        Assert.Throws<JsonException>(() => MicrosoftAuthenticator.ResolveMSGraphResult("not-json",
            SerializerContext.Default.GraphAuthResultModel));
    }

    [TestMethod]
    public async Task PendingAuthorizationContinuesUntilTokenArrives()
    {
        using var factory = CreateFactory(DeviceJson,
            """{"error":"authorization_pending","error_description":"Waiting"}""", TokenJson);
        DeviceIdResponseModel? notified = null;

        var result = await MicrosoftAuthenticator.GetMSAuthResult(factory, model => notified = model);

        Assert.IsNotNull(result);
        Assert.AreEqual("access", result.AccessToken);
        Assert.IsNotNull(notified);
        Assert.AreEqual("user", notified.UserCode);
        Assert.AreEqual(3, factory.RequestCount);
    }

    [TestMethod]
    [DataRow("authorization_declined")]
    [DataRow("expired_token")]
    [DataRow("bad_verification_code")]
    [DataRow("temporarily_unavailable")]
    public async Task OAuthFailureEndsPollingWithoutCastingAnErrorToAToken(string error)
    {
        using var factory = CreateFactory(DeviceJson,
            $$"""{"error":"{{error}}","error_description":"Failed"}""");

        var result = await MicrosoftAuthenticator.GetMSAuthResult(factory, _ => { });

        Assert.IsNull(result);
        Assert.AreEqual(2, factory.RequestCount);
    }

    [TestMethod]
    public async Task NullTokenResponseEndsPollingWithoutCrashing()
    {
        using var factory = CreateFactory(DeviceJson, "null");
        var result = await MicrosoftAuthenticator.GetMSAuthResult(factory, _ => { });

        Assert.IsNull(result);
        Assert.AreEqual(2, factory.RequestCount);
    }

    [TestMethod]
    public async Task DeviceRequestFailureDoesNotNotifyOrPoll()
    {
        using var factory = CreateFactory("""{"error":"invalid_client","error_description":"Bad client"}""");
        var notified = false;
        var result = await MicrosoftAuthenticator.GetMSAuthResult(factory, _ => notified = true);

        Assert.IsNull(result);
        Assert.IsFalse(notified);
        Assert.AreEqual(1, factory.RequestCount);
    }

    private static ResponseFactory CreateFactory(params string[] responses)
    {
        MicrosoftAuthenticator.Configure(new MicrosoftAuthenticatorAPISettings
        {
            ClientId = "test-client",
            TenentId = "consumers",
            Scopes = ["XboxLive.signin"]
        });
        return new ResponseFactory(responses);
    }

    private sealed class ResponseFactory(string[] responses) : IHttpClientFactory, IDisposable
    {
        private readonly ResponseHandler _handler = new(responses);
        private HttpClient? _client;
        public int RequestCount => _handler.RequestCount;
        public HttpClient CreateClient(string name) => _client ??= new HttpClient(_handler);
        public void Dispose() => _client?.Dispose();
    }

    private sealed class ResponseHandler(string[] responses) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RequestCount >= responses.Length)
                throw new InvalidOperationException("The authenticator made an unexpected extra request.");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responses[RequestCount++])
            });
        }
    }
}
