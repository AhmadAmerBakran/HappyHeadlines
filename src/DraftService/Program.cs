using DraftService.Data;
using HappyHeadlines.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("DraftService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IDraftRepository, DraftRepository>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHappyHeadlinesRequestLogging();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    instance = Environment.MachineName
}));

await DatabaseInitializer.InitialiseAsync(app.Services, app.Lifetime.ApplicationStopping);

app.Run();
