using HappyHeadlines.Observability;
using NewsletterService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("NewsletterService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<NewsletterStore>();
builder.Services.AddHostedService<PublishedArticleConsumer>();
builder.Services.AddHttpClient<ArticleClient>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ArticleService"] ?? "http://localhost:8080/");
});

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
