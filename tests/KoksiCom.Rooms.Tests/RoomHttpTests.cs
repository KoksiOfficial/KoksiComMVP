using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using KoksiCom.Api.Guest;
using KoksiCom.Api.Rooms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace KoksiCom.Rooms.Tests;

public class RoomHttpTests
{
    private const string Password = "Abcd12";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task List_starts_empty()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");

        var response = await ada.GetAsync("/api/rooms");
        var body = await response.Content.ReadFromJsonAsync<RoomListBody>(Json);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body!.Rooms);
    }

    [Fact]
    public async Task Create_valid_room_lists_one_user()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");

        var created = await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        var createdBody = await created.Content.ReadFromJsonAsync<NameBody>(Json);
        var list = await ada.GetFromJsonAsync<RoomListBody>("/api/rooms", Json);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("Lab1", createdBody?.Name);
        Assert.Single(list!.Rooms);
        Assert.Equal("Lab1", list.Rooms[0].Name);
        Assert.Equal(1, list.Rooms[0].Users);
    }

    [Theory]
    [InlineData("A", "Abcd12")]
    [InlineData("Lab1", "Ab1")]
    [InlineData("Lab1", "abcdef1")]
    public async Task Create_invalid_name_or_password_returns_400_invalid(string name, string pwd)
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");

        var response = await ada.PostAsJsonAsync("/api/rooms", new { name, pwd });
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid", body?.Error);
    }

    [Fact]
    public async Task Create_taken_name_returns_400_taken()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        var bob = await host.LoginAsync("Bob3");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        var response = await bob.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("taken", body?.Error);
    }

    [Fact]
    public async Task Join_correct_password_increments_users()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        var bob = await host.LoginAsync("Bob3");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        var joined = await bob.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });
        var list = await ada.GetFromJsonAsync<RoomListBody>("/api/rooms", Json);

        Assert.Equal(HttpStatusCode.OK, joined.StatusCode);
        Assert.Equal(2, list!.Rooms[0].Users);
    }

    [Fact]
    public async Task Join_wrong_password_returns_403()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        var bob = await host.LoginAsync("Bob3");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        var response = await bob.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = "Xyza98" });
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("forbidden", body?.Error);
    }

    [Fact]
    public async Task Join_unknown_room_returns_404()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");

        var response = await ada.PostAsJsonAsync("/api/rooms/join", new { name = "NoRoom1", pwd = Password });
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("not_found", body?.Error);
    }

    [Fact]
    public async Task Join_invalid_password_format_returns_400_invalid()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        var bob = await host.LoginAsync("Bob3");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        var response = await bob.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = "ab1" });
        var body = await response.Content.ReadFromJsonAsync<ErrorBody>(Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid", body?.Error);
    }

    [Fact]
    public async Task Create_or_join_while_in_room_returns_400_in_room()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        var bob = await host.LoginAsync("Bob3");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await bob.PostAsJsonAsync("/api/rooms", new { name = "Lab2", pwd = Password });

        var createAgain = await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab3", pwd = Password });
        var joinOther = await ada.PostAsJsonAsync("/api/rooms/join", new { name = "Lab2", pwd = Password });

        Assert.Equal("in_room", (await createAgain.Content.ReadFromJsonAsync<ErrorBody>(Json))?.Error);
        Assert.Equal("in_room", (await joinOther.Content.ReadFromJsonAsync<ErrorBody>(Json))?.Error);
        Assert.Equal(HttpStatusCode.BadRequest, createAgain.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, joinOther.StatusCode);
    }

    [Fact]
    public async Task Leave_last_member_removes_room_and_name_can_be_reused()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        var leave = await ada.PostAsync("/api/rooms/leave", null);
        var list = await ada.GetFromJsonAsync<RoomListBody>("/api/rooms", Json);
        var createAgain = await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        Assert.Equal(HttpStatusCode.OK, leave.StatusCode);
        Assert.Empty(list!.Rooms);
        Assert.Equal(HttpStatusCode.OK, createAgain.StatusCode);
    }

    [Fact]
    public async Task Leave_while_others_remain_keeps_room_and_allows_creator_to_leave()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        var bob = await host.LoginAsync("Bob3");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        await bob.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password });

        var leave = await ada.PostAsync("/api/rooms/leave", null);
        var list = await bob.GetFromJsonAsync<RoomListBody>("/api/rooms", Json);
        var bobRoom = await bob.GetFromJsonAsync<NameBody>("/api/rooms/me", Json);

        Assert.Equal(HttpStatusCode.OK, leave.StatusCode);
        Assert.Single(list!.Rooms);
        Assert.Equal(1, list.Rooms[0].Users);
        Assert.Equal("Lab1", bobRoom?.Name);
    }

    [Fact]
    public async Task Rooms_me_in_room_vs_not()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");

        var before = await ada.GetAsync("/api/rooms/me");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });
        var after = await ada.GetAsync("/api/rooms/me");
        var body = await after.Content.ReadFromJsonAsync<NameBody>(Json);

        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.True(string.IsNullOrEmpty(await before.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal("Lab1", body?.Name);
    }

    [Fact]
    public async Task Unauthenticated_room_calls_return_401()
    {
        await using var host = await RoomHost.StartAsync();
        var anon = host.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/rooms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsJsonAsync("/api/rooms/join", new { name = "Lab1", pwd = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.PostAsync("/api/rooms/leave", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/rooms/me")).StatusCode);
    }

    [Fact]
    public async Task List_does_not_include_password_or_member_names()
    {
        await using var host = await RoomHost.StartAsync();
        var ada = await host.LoginAsync("Ada2");
        await ada.PostAsJsonAsync("/api/rooms", new { name = "Lab1", pwd = Password });

        var json = await (await ada.GetAsync("/api/rooms")).Content.ReadAsStringAsync();

        Assert.DoesNotContain(Password, json, StringComparison.Ordinal);
        Assert.DoesNotContain("pwd", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Ada2", json, StringComparison.Ordinal);
        Assert.Contains("Lab1", json, StringComparison.Ordinal);
    }

    private sealed record TokenBody(string Token);
    private sealed record ErrorBody(string Error);
    private sealed record NameBody(string Name);
    private sealed record RoomListBody(RoomRow[] Rooms);
    private sealed record RoomRow(string Name, int Users);

    private sealed class RoomHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly Uri _baseAddress;

        private RoomHost(WebApplication app, Uri baseAddress)
        {
            _app = app;
            _baseAddress = baseAddress;
        }

        public static async Task<RoomHost> StartAsync()
        {
            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.UseKestrel();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddGuest();
            builder.Services.AddRooms();

            var app = builder.Build();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapGuest();
            app.MapRooms();
            await app.StartAsync();

            return new RoomHost(app, new Uri(app.Urls.Single()));
        }

        public HttpClient CreateClient() => new() { BaseAddress = _baseAddress };

        public async Task<HttpClient> LoginAsync(string name)
        {
            var client = CreateClient();
            var login = await client.PostAsJsonAsync("/api/guest/login", new { name });
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<TokenBody>(Json))!.Token;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        public async ValueTask DisposeAsync()
        {
            await _app.DisposeAsync();
        }
    }
}
