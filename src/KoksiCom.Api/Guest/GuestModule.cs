using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace KoksiCom.Api.Guest;

public static class GuestModule
{
    public static IServiceCollection AddGuest(
        this IServiceCollection services,
        GuestRegistry? registry = null,
        GuestTokens? tokens = null,
        TimeProvider? time = null)
    {
        var clock = time ?? TimeProvider.System;
        var guests = registry ?? new GuestRegistry();
        var guestTokens = tokens ?? new GuestTokens();

        services.AddSingleton(clock);
        services.AddSingleton(guests);
        services.AddSingleton(guestTokens);
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = guestTokens.ValidationParameters;
            });
        services.AddAuthorization();
        return services;
    }

    public static WebApplication MapGuest(this WebApplication app)
    {
        app.MapPost("/api/guest/login", (
            LoginRequest? body,
            GuestRegistry registry,
            GuestTokens tokens,
            TimeProvider time) =>
        {
            var name = body?.Name;
            if (!DisplayName.IsValid(name) || name is null)
            {
                return Results.BadRequest(new { error = "invalid" });
            }

            var now = time.GetUtcNow();
            var expiresAt = now.AddHours(GuestTokens.LifetimeHours);
            if (!registry.TryAdd(name, expiresAt, now))
            {
                return Results.BadRequest(new { error = "taken" });
            }

            return Results.Ok(new { token = tokens.Issue(name, now) });
        });

        app.MapGet("/api/guest/me", (
            HttpContext http,
            GuestRegistry registry,
            TimeProvider time) =>
        {
            var name = http.User.FindFirst(GuestTokens.NameClaim)?.Value;
            var now = time.GetUtcNow();
            if (string.IsNullOrEmpty(name) || !registry.ContainsActive(name, now))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new { name });
        }).RequireAuthorization();

        return app;
    }
}

public sealed record LoginRequest(string? Name);
