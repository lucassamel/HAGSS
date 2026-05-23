using HAGSS.Data;
using HAGSS.Endpoints;
using HAGSS.Extensions;
using HAGSS.Hubs;
using HAGSS.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddHagssInfrastructure(builder.Configuration);

var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health");
app.MapHub<ReservationHub>(ReservationHub.Path);
app.MapReservationEndpoints();
app.MapActivityEndpoints();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
    await DatabaseInitializer.InitializeAsync(db, logger, CancellationToken.None);
}

app.Run();
