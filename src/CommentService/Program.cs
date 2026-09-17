using CommentService.Data;
using CommentService.Services;
using HappyHeadlines.Observability;
using Polly;
using Polly.CircuitBreaker;

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("CommentService");

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<ICommentRepository, CommentRepository>();

var profanityServiceUrl = builder.Configuration["Services:ProfanityService"]
    ?? throw new InvalidOperationException("Services:ProfanityService is missing.");

builder.Services.AddSingleton<AsyncCircuitBreakerPolicy<HttpResponseMessage>>(
    Policy<HttpResponseMessage>
        .Handle<HttpRequestException>()
        .Or<TaskCanceledException>()
        .OrResult(response => (int)response.StatusCode >= 500)
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30)));

builder.Services.AddHttpClient<IProfanityClient, ProfanityClient>(client =>
{
    client.BaseAddress = new Uri(profanityServiceUrl);
    client.Timeout = TimeSpan.FromSeconds(3);
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseHappyHeadlinesRequestLogging();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-CommentService-Instance"] = Environment.MachineName;
    await next();
});

app.MapControllers();

app.MapGet(
    "/health",
    (AsyncCircuitBreakerPolicy<HttpResponseMessage> circuitBreaker) => Results.Ok(new
    {
        status = "ok",
        profanityCircuit = circuitBreaker.CircuitState.ToString(),
        instance = Environment.MachineName
    }));

await DatabaseInitializer.InitialiseAsync(app.Services, app.Lifetime.ApplicationStopping);

app.Run();
