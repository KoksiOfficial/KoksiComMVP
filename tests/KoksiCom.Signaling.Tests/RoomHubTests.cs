using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KoksiCom.Api.Guest;
using KoksiCom.Api.Rooms;
using KoksiCom.Api.Signaling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace KoksiCom.Signaling.Tests;

public class RoomHubTests
{
    private const string Password = "Abcd12";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task Hub_without_jwt_is_not_authorized()
    {
        await using var host = await HubHost.StartAsync();
        await using var connection = host.Connect(token: null);

        var ex = await Assert.ThrowsAnyAsync<Exception>(() => connection.StartAsync());
        var text = ex.ToString();

        Assert.True(
            text.Contains("401", StringComparison.Ordinal)
            || text.Contains("Unauthorized", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Enter_when_not_in_a_room_errors()
    {
        await using var host = await HubHost.StartAsync();
        var ada = await host.GuestAsync("Ada2");
        await using var connection = ada.Hub;
        await connection.StartAsync();

        var ex = await Assert.ThrowsAsync<HubException>(() => connection.InvokeAsync("Enter"));

        Assert.Contains("not_in_room", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Two_guests_enter_see_each_other()
    {
        await using var host = await HubHost.StartAsync();
        var ada = await host.GuestAsync("Ada2");
        var bob = await host.GuestAsync("Bob3");
        await ada.Http.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await bob.Http.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });

        var adaJoined = new TaskCompletionSource<PeerDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bobPeers = new TaskCompletionSource<List<PeerDto>>(TaskCreationOptions.RunContinuationsAsynchronously);
        ada.Hub.On<PeerDto>("peerJoined", peer => adaJoined.TrySetResult(peer));
        bob.Hub.On<List<PeerDto>>("peers", list => bobPeers.TrySetResult(list));

        await ada.Hub.StartAsync();
        await ada.Hub.InvokeAsync("Enter");
        await bob.Hub.StartAsync();
        await bob.Hub.InvokeAsync("Enter");

        var joined = await adaJoined.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var roster = await bobPeers.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Bob3", joined.Name);
        Assert.Contains(roster, peer => peer.Name == "Ada2");
        Assert.Contains(roster, peer => peer.Name == "Bob3");
        Assert.False(joined.Mic);
        Assert.True(joined.Headphones);
        Assert.False(joined.Share);
    }

    [Fact]
    public async Task Signal_reaches_named_peer_only()
    {
        await using var host = await HubHost.StartAsync();
        var ada = await host.GuestAsync("Ada2");
        var bob = await host.GuestAsync("Bob3");
        var cal = await host.GuestAsync("Cal4");
        await ada.Http.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await bob.Http.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });
        await cal.Http.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });

        await ada.Hub.StartAsync();
        await ada.Hub.InvokeAsync("Enter");
        await bob.Hub.StartAsync();
        await bob.Hub.InvokeAsync("Enter");
        await cal.Hub.StartAsync();
        await cal.Hub.InvokeAsync("Enter");

        var bobGot = new TaskCompletionSource<(string From, JsonElement Payload)>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calGot = new TaskCompletionSource<(string From, JsonElement Payload)>(TaskCreationOptions.RunContinuationsAsynchronously);
        bob.Hub.On<string, JsonElement>("signal", (from, payload) => bobGot.TrySetResult((from, payload)));
        cal.Hub.On<string, JsonElement>("signal", (from, payload) => calGot.TrySetResult((from, payload)));

        await ada.Hub.InvokeAsync("Signal", "Bob3", new { sdp = "v=0", type = "offer" });

        var received = await bobGot.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(300);

        Assert.Equal("Ada2", received.From);
        Assert.Equal("v=0", received.Payload.GetProperty("sdp").GetString());
        Assert.False(calGot.Task.IsCompleted);
    }

    [Fact]
    public async Task SetState_notifies_the_other_client()
    {
        await using var host = await HubHost.StartAsync();
        var ada = await host.GuestAsync("Ada2");
        var bob = await host.GuestAsync("Bob3");
        await ada.Http.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await bob.Http.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });
        await ada.Hub.StartAsync();
        await ada.Hub.InvokeAsync("Enter");
        await bob.Hub.StartAsync();
        await bob.Hub.InvokeAsync("Enter");

        var bobState = new TaskCompletionSource<(string Name, PeerDto State)>(TaskCreationOptions.RunContinuationsAsynchronously);
        bob.Hub.On<string, PeerDto>("peerState", (name, state) => bobState.TrySetResult((name, state)));

        await ada.Hub.InvokeAsync("SetState", true, false, true);
        var update = await bobState.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("Ada2", update.Name);
        Assert.True(update.State.Mic);
        Assert.False(update.State.Headphones);
        Assert.True(update.State.Share);
    }

    [Fact]
    public async Task Dropped_connection_frees_guest_name_and_empty_room()
    {
        await using var host = await HubHost.StartAsync();
        var ada = await host.GuestAsync("Ada2");
        var bob = await host.GuestAsync("Bob3");
        await ada.Http.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await ada.Hub.StartAsync();
        await ada.Hub.InvokeAsync("Enter");

        await ada.Hub.StopAsync();
        await WaitUntil(async () =>
        {
            var list = await bob.Http.GetFromJsonAsync<RoomListBody>("/api/rooms", Json);
            return list is { Rooms.Length: 0 };
        });

        var login = await host.CreateClient().PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var rooms = await bob.Http.GetFromJsonAsync<RoomListBody>("/api/rooms", Json);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Empty(rooms!.Rooms);
    }

    [Fact]
    public async Task Intentional_leave_keeps_guest_name_taken()
    {
        await using var host = await HubHost.StartAsync();
        var ada = await host.GuestAsync("Ada2");
        await ada.Http.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await ada.Hub.StartAsync();
        await ada.Hub.InvokeAsync("Enter");
        await ada.Hub.InvokeAsync("Leave");
        await ada.Hub.StopAsync();

        var login = await host.CreateClient().PostAsJsonAsync("/api/guest/login", new { name = "Ada2" });
        var body = await login.Content.ReadFromJsonAsync<ErrorBody>(Json);
        var me = await ada.Http.GetAsync("/api/guest/me");

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
        Assert.Equal("taken", body?.Error);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Signal_does_not_write_payload_files()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "koksicom-signal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(contentRoot);
        try
        {
            await using var host = await HubHost.StartAsync(contentRoot);
            var ada = await host.GuestAsync("Ada2");
            var bob = await host.GuestAsync("Bob3");
            await ada.Http.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
            await bob.Http.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });
            await ada.Hub.StartAsync();
            await ada.Hub.InvokeAsync("Enter");
            await bob.Hub.StartAsync();
            await bob.Hub.InvokeAsync("Enter");

            var bobGot = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            bob.Hub.On<string, JsonElement>("signal", (_, _) => bobGot.TrySetResult(true));
            await ada.Hub.InvokeAsync("Signal", "Bob3", new { sdp = "offer-body", chat = "hello" });
            await bobGot.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var files = Directory.Exists(contentRoot)
                ? Directory.GetFiles(contentRoot, "*", SearchOption.AllDirectories)
                : [];
            Assert.Empty(files);
        }
        finally
        {
            if (Directory.Exists(contentRoot))
            {
                Directory.Delete(contentRoot, recursive: true);
            }
        }
    }

    private static async Task WaitUntil(Func<Task<bool>> probe)
    {
        for (var i = 0; i < 50; i++)
        {
            if (await probe())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("timed out waiting for hub disconnect side effects");
    }

    private sealed record TokenBody(string Token);
    private sealed record ErrorBody(string Error);
    private sealed record RoomListBody(RoomRow[] Rooms);
    private sealed record RoomRow(string Name, int Users);

    private sealed class PeerDto
    {
        public string Name { get; set; } = "";
        public bool Mic { get; set; }
        public bool Headphones { get; set; }
        public bool Share { get; set; }
    }

    private sealed class HubHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Uri _baseAddress;
        private readonly List<IAsyncDisposable> _hubs = [];

        private HubHost(WebApplication app, Uri baseAddress)
        {
            _app = app;
            _baseAddress = baseAddress;
        }

        public static async Task<HubHost> StartAsync(string? contentRoot = null)
        {
            var builder = contentRoot is null
                ? WebApplication.CreateBuilder()
                : WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = contentRoot });
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls("http://127.0.0.1:0");

            builder.Services.AddGuest();
            builder.Services.AddRooms();
            builder.Services.AddSignaling();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGuest();
            app.MapRooms();
            app.MapSignaling();
            await app.StartAsync();

            return new HubHost(app, new Uri(app.Urls.Single()));
        }

        public HttpClient CreateClient() => new() { BaseAddress = _baseAddress };

        public HubConnection Connect(string? token)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl(new Uri(_baseAddress, "/hubs/room"), options =>
                {
                    if (token is not null)
                    {
                        options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                    }
                })
                .Build();
            _hubs.Add(connection);
            return connection;
        }

        public async Task<GuestSession> GuestAsync(string name)
        {
            var client = CreateClient();
            var login = await client.PostAsJsonAsync("/api/guest/login", new { name });
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<TokenBody>(Json))!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return new GuestSession(client, token, Connect(token));
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var hub in _hubs)
            {
                await hub.DisposeAsync();
            }

            await _app.DisposeAsync();
        }
    }

    private sealed record GuestSession(HttpClient Http, string Token, HubConnection Hub);
}
