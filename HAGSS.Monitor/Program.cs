using HAGSS.Monitor.Components;
using HAGSS.Monitor.Options;
using HAGSS.Monitor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MonitorOptions>(builder.Configuration.GetSection(MonitorOptions.SectionName));

var apiBaseUrl = builder.Configuration.GetSection(MonitorOptions.SectionName)["ApiBaseUrl"]
    ?? "http://localhost:8080";

builder.Services.AddHttpClient("hagss-api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
});

builder.Services.AddSingleton<ActivityFeed>();
builder.Services.AddHostedService<ApiActivityListener>();
builder.Services.AddHostedService<SeatSnapshotRefreshWorker>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
