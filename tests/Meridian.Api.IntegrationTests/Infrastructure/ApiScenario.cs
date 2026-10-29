using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Meridian.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Per-scenario state shared by step definition classes (Reqnroll context injection): the host, an HTTPS client
/// with a cookie jar (the session cookie is Secure), the last response, and the antiforgery token.
/// </summary>
public sealed class ApiScenario : IAsyncDisposable
{
    public const string XsrfHeader = "X-XSRF-TOKEN";
    private const string XsrfCookie = "XSRF-TOKEN";

    private MeridianApiFactory? _factory;
    private HttpClient? _client;

    public MeridianApiFactory Factory => _factory ?? throw new InvalidOperationException("Start the app in a Given step first.");
    public HttpClient Client => _client ?? throw new InvalidOperationException("Start the app in a Given step first.");
    public HttpResponseMessage? Response { get; private set; }
    public HttpResponseMessage LastResponse => Response ?? throw new InvalidOperationException("Send a request first.");

    public void Start(MeridianApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public async Task<HttpResponseMessage> GetAsync(string path) => Track(await Client.GetAsync(new Uri(path, UriKind.Relative)));

    /// <summary>POSTs JSON the way the SPA does: with the antiforgery token in <see cref="XsrfHeader"/> unless told otherwise.</summary>
    public async Task<HttpResponseMessage> PostAsync(string path, object? body, bool withAntiforgeryToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { }) };
        if (withAntiforgeryToken)
        {
            request.Headers.Add(XsrfHeader, await GetAntiforgeryTokenAsync());
        }

        return Track(await Client.SendAsync(request));
    }

    /// <summary>Sends a request built by the caller (custom headers) and tracks its response.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request) => Track(await Client.SendAsync(request));

    public async Task<string> GetAntiforgeryTokenAsync()
    {
        using var response = await Client.GetAsync(new Uri("/api/v1/auth/antiforgery", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith($"{XsrfCookie}=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(cookie[(XsrfCookie.Length + 1)..cookie.IndexOf(';', StringComparison.Ordinal)]);
    }

    private HttpResponseMessage Track(HttpResponseMessage response)
    {
        Response?.Dispose();
        Response = response;
        return response;
    }

    public async ValueTask DisposeAsync()
    {
        Response?.Dispose();
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }
}
