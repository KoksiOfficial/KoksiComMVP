using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using KoksiCom.Api.Guest;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace KoksiCom.Guest.Tests;

public class GuestHttpTests
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Login_valid_name_returns_jwt_with_that_name()
    {
        await using var host = await GuestHost.StartAsync();

        var response = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var body = await response.Content.ReadFromJsonAsync<TokenBody>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace(body?.Token));
        Assert.True(host.Tokens.TryReadName(body!.Token, TimeProvider.System.GetUtcNow(), out var name));
        Assert.Equal("Ada2", name);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Abcdefghijk")]
    [InlineData("Ada 2")]
    [InlineData("Ada-2")]
    public async Task Login_invalid_name_returns_400_invalid(string name)
    {
        await using var host = await GuestHost.StartAsync();

        var response = await host.Client.PostAsJsonAsync("/api/guest/login", new { name });
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid", body?.Error);
    }

    [Fact]
    public async Task Login_taken_name_returns_400_taken()
    {
        await using var host = await GuestHost.StartAsync();

        var first = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var second = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var body = await second.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
        Assert.Equal("taken", body?.Error);
    }

    [Fact]
    public async Task Login_different_valid_names_both_succeed()
    {
        await using var host = await GuestHost.StartAsync();

        var first = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var second = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Bob3" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Me_with_good_token_returns_name()
    {
        await using var host = await GuestHost.StartAsync();
        var login = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var token = (await login.Content.ReadFromJsonAsync<TokenBody>(Json))!.Token;
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await host.Client.GetAsync("/api/guest/me");
        var body = await response.Content.ReadFromJsonAsync<MeBody>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Ada2", body?.Name);
    }

    [Fact]
    public async Task Me_without_token_returns_401()
    {
        await using var host = await GuestHost.StartAsync();

        var response = await host.Client.GetAsync("/api/guest/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(string.IsNullOrEmpty(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Me_with_garbage_token_returns_401()
    {
        await using var host = await GuestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        var response = await host.Client.GetAsync("/api/guest/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_token_from_previous_key_returns_401()
    {
        var oldTokens = new GuestTokens(RandomNumberGenerator.GetBytes(32));
        var jwt = oldTokens.Issue("Ada2", TimeProvider.System.GetUtcNow());

        await using var host = await GuestHost.StartAsync(
            tokens: new GuestTokens(RandomNumberGenerator.GetBytes(32)));
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await host.Client.GetAsync("/api/guest/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_reuses_name_after_registry_remove()
    {
        await using var host = await GuestHost.StartAsync();
        var first = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        host.Registry.Remove("Ada2");
        var second = await host.Client.PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    private sealed record TokenBody(string Token);
    private sealed record ErrorBody(string Error);
    private sealed record MeBody(string Name);

    private sealed class GuestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private GuestHost(WebApplication app, HttpClient client, GuestRegistry registry, GuestTokens tokens)
        {
            _app = app;
            Client = client;
            Registry = registry;
            Tokens = tokens;
        }

        public HttpClient Client { get; }
        public GuestRegistry Registry { get; }
        public GuestTokens Tokens { get; }

        public static async Task<GuestHost> StartAsync(
            GuestRegistry? registry = null,
            GuestTokens? tokens = null,
            TimeProvider? time = null)
        {
            var clock = time ?? TimeProvider.System;
            var guests = registry ?? new GuestRegistry();
            var guestTokens = tokens ?? new GuestTokens();

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddGuest(guests, guestTokens, clock);

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGuest();
            await app.StartAsync();

            var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            return new GuestHost(app, client, guests, guestTokens);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.DisposeAsync();
        }
    }
}
