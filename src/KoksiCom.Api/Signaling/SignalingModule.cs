using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace KoksiCom.Api.Signaling;

public static class SignalingModule
{
    public static IServiceCollection AddSignaling(this IServiceCollection services)
    {
        services.AddSingleton<RoomPresence>();
        services.AddSignalR();
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events?.OnMessageReceived;
            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = async context =>
            {
                if (previous is not null)
                {
                    await previous(context);
                }

                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/room"))
                {
                    context.Token = accessToken;
                }
            };
        });
        return services;
    }

    public static WebApplication MapSignaling(this WebApplication app)
    {
        app.MapHub<RoomHub>("/hubs/room").RequireAuthorization();
        return app;
    }
}
