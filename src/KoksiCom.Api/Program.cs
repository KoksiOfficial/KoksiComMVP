using KoksiCom.Api;
using KoksiCom.Api.Guest;
using KoksiCom.Api.Rooms;
using KoksiCom.Api.Signaling;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGuest();
builder.Services.AddRooms();
builder.Services.AddSignaling();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = LanDevCertificate.HttpsPort;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(https =>
    {
        https.ServerCertificate = LanDevCertificate.Create();
    });
});

var app = builder.Build();

app.Use(async (context, next) =>
{
    context.Response.Headers["Permissions-Policy"] = "microphone=*, display-capture=*";
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapGuest();
app.MapRooms();
app.MapSignaling();

app.Run();
