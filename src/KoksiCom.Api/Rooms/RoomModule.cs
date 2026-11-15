using KoksiCom.Api.Guest;

namespace KoksiCom.Api.Rooms;

public static class RoomModule
{
    public static IServiceCollection AddRooms(this IServiceCollection services, RoomRegistry? rooms = null)
    {
        services.AddSingleton(rooms ?? new RoomRegistry());
        return services;
    }

    public static WebApplication MapRooms(this WebApplication app)
    {
        app.MapGet("/api/rooms", (
            HttpContext http,
            RoomRegistry rooms,
            GuestRegistry guests,
            TimeProvider time) =>
        {
            if (ActiveGuest(http, guests, time) is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { rooms = rooms.List().Select(item => new { name = item.Name, users = item.Users }) });
        }).RequireAuthorization();

        app.MapPost("/api/rooms", (
            RoomRequest? body,
            HttpContext http,
            RoomRegistry rooms,
            GuestRegistry guests,
            TimeProvider time) =>
        {
            var guest = ActiveGuest(http, guests, time);
            if (guest is null)
            {
                return Results.Unauthorized();
            }

            return rooms.TryCreate(guest, body?.Name, body?.Pwd) switch
            {
                CreateResult.Created => Results.Ok(new { name = body!.Name }),
                CreateResult.Taken => Results.BadRequest(new { error = "taken" }),
                CreateResult.InRoom => Results.BadRequest(new { error = "in_room" }),
                _ => Results.BadRequest(new { error = "invalid" })
            };
        }).RequireAuthorization();

        app.MapPost("/api/rooms/join", (
            RoomRequest? body,
            HttpContext http,
            RoomRegistry rooms,
            GuestRegistry guests,
            TimeProvider time) =>
        {
            var guest = ActiveGuest(http, guests, time);
            if (guest is null)
            {
                return Results.Unauthorized();
            }

            return rooms.TryJoin(guest, body?.Name, body?.Pwd) switch
            {
                JoinResult.Joined => Results.Ok(new { name = body!.Name }),
                JoinResult.InRoom => Results.BadRequest(new { error = "in_room" }),
                JoinResult.NotFound => Results.Json(new { error = "not_found" }, statusCode: StatusCodes.Status404NotFound),
                JoinResult.Forbidden => Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden),
                _ => Results.BadRequest(new { error = "invalid" })
            };
        }).RequireAuthorization();

        app.MapPost("/api/rooms/leave", (
            HttpContext http,
            RoomRegistry rooms,
            GuestRegistry guests,
            TimeProvider time) =>
        {
            var guest = ActiveGuest(http, guests, time);
            if (guest is null)
            {
                return Results.Unauthorized();
            }

            return rooms.TryLeave(guest) switch
            {
                LeaveResult.Left => Results.Ok(),
                _ => Results.BadRequest(new { error = "not_in_room" })
            };
        }).RequireAuthorization();

        app.MapGet("/api/rooms/me", (
            HttpContext http,
            RoomRegistry rooms,
            GuestRegistry guests,
            TimeProvider time) =>
        {
            var guest = ActiveGuest(http, guests, time);
            if (guest is null)
            {
                return Results.Unauthorized();
            }

            var room = rooms.RoomForGuest(guest);
            return room is null ? Results.NotFound() : Results.Ok(new { name = room });
        }).RequireAuthorization();

        return app;
    }

    private static string? ActiveGuest(HttpContext http, GuestRegistry guests, TimeProvider time)
    {
        var name = http.User.FindFirst(GuestTokens.NameClaim)?.Value;
        if (string.IsNullOrEmpty(name) || !guests.ContainsActive(name, time.GetUtcNow()))
        {
            return null;
        }

        return name;
    }
}

public sealed record RoomRequest(string? Name, string? Pwd);
