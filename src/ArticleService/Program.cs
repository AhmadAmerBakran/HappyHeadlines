using ArticleService.Cache;
using ArticleService.Data;
using ArticleService.Messaging;
using HappyHeadlines.Observability;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("ArticleService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IArticleShardResolver, ArticleShardResolver>();
builder.Services.AddScoped<IArticleRepository, ArticleRepository>();
builder.Services.AddHostedService<PublishedArticleConsumer>();

var articleCacheConnection = builder.Configuration.GetConnectionString("ArticleCache")
    ?? "article-cache:6379";
var articleCacheOptions = ConfigurationOptions.Parse(articleCacheConnection);
articleCacheOptions.AbortOnConnectFail = false;

builder.Services.AddSingleton<IConnectionMultiplexer>(
    _ => ConnectionMultiplexer.Connect(articleCacheOptions));
builder.Services.AddSingleton<ArticleCache>();
builder.Services.AddHostedService<ArticleCacheRefreshWorker>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHappyHeadlinesRequestLogging();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-ArticleService-Instance"] = Environment.MachineName;
    await next();
});

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    instance = Environment.MachineName
}));

await DatabaseInitializer.InitialiseAsync(app.Services, app.Lifetime.ApplicationStopping);

app.Run();
