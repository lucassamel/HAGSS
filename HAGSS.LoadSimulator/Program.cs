using HAGSS.LoadSimulator.Options;
using HAGSS.LoadSimulator.Workers;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<SimulatorOptions>(builder.Configuration.GetSection(SimulatorOptions.SectionName));

var apiBaseUrl = builder.Configuration.GetSection(SimulatorOptions.SectionName)["ApiBaseUrl"]
    ?? "http://localhost:8080";

builder.Services.AddHttpClient("hagss-api", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHostedService<LoadSimulationWorker>();

await builder.Build().RunAsync();
