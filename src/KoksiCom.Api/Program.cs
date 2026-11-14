using KoksiCom.Api.Guest;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGuest();

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

app.Run();
