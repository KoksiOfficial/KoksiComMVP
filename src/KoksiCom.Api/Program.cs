using KoksiCom.Api.Guest;
using KoksiCom.Api.Rooms;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGuest();
builder.Services.AddRooms();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapGuest();
app.MapRooms();

app.Run();
