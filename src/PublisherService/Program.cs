using HappyHeadlines.Observability;
using PublisherService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("PublisherService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ArticlePublisher>();

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

app.Run();
