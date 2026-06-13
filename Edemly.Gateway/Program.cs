var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseWebSockets();

app.MapGet("/gateway/health", () => Results.Ok(new
{
    status = "ok",
    service = "Edemly.Gateway"
}));

app.MapReverseProxy();

app.Run();
