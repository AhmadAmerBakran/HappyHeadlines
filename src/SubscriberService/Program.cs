using System.Net.Mail;
using HappyHeadlines.Observability;
using SubscriberService;
using SubscriberService.Data;

var builder = WebApplication.CreateBuilder(args);
builder.AddHappyHeadlinesObservability("SubscriberService");
builder.Services.AddScoped<SubscriberRepository>();
builder.Services.AddHostedService<SubscriberOutboxWorker>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHappyHeadlinesRequestLogging();

app.MapGet("/health", () => Results.Ok(new { status = "ok", instance = Environment.MachineName }));

app.MapGet("/api/subscribers/rollout", async (
    HttpRequest request, SubscriberRepository repository, IConfiguration configuration, CancellationToken ct) =>
{
    if (!HasKey(request, configuration["Subscriber:AdminKey"], "X-Subscriber-Admin-Key"))
        return Results.Unauthorized();

    return Results.Ok(await repository.GetRolloutAsync(ct));
});

app.MapPut("/api/subscribers/rollout", async (
    HttpRequest request, UpdateRolloutRequest update, SubscriberRepository repository,
    IConfiguration configuration, CancellationToken ct) =>
{
    if (!HasKey(request, configuration["Subscriber:AdminKey"], "X-Subscriber-Admin-Key"))
        return Results.Unauthorized();

    if (update.Percentage is < 0 or > 100)
        return Results.BadRequest(new { error = "Percentage must be between 0 and 100." });

    var rollout = new ReleaseRollout(update.Enabled, update.Percentage);
    await repository.UpdateRolloutAsync(rollout, ct);
    return Results.Ok(rollout);
});

app.MapPost("/api/subscribers", async (
    SubscribeRequest input, SubscriberRepository repository, CancellationToken ct) =>
{
    if (!TryNormaliseEmail(input.Email, out var email))
        return Results.BadRequest(new { error = "A valid email address is required." });

    var rollout = await repository.GetRolloutAsync(ct);
    if (!rollout.Allows(email))
        return Results.NotFound();

    var result = await repository.SubscribeAsync(email, ct);
    return result.Created
        ? Results.Created("/api/subscribers", new { id = result.Id, email })
        : Results.Ok(new { email, status = "already subscribed" });
});

app.MapDelete("/api/subscribers/{email}", async (
    string email, SubscriberRepository repository, CancellationToken ct) =>
{
    if (!TryNormaliseEmail(email, out var normalised))
        return Results.BadRequest(new { error = "A valid email address is required." });

    var rollout = await repository.GetRolloutAsync(ct);
    if (!rollout.Allows(normalised))
        return Results.NotFound();

    return await repository.UnsubscribeAsync(normalised, ct)
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapGet("/api/subscribers", async (
    HttpRequest request, SubscriberRepository repository, IConfiguration configuration, CancellationToken ct) =>
{
    if (!HasKey(request, configuration["Subscriber:InternalKey"], "X-Subscriber-Internal-Key"))
        return Results.Unauthorized();

    var rollout = await repository.GetRolloutAsync(ct);
    if (!rollout.Enabled || rollout.Percentage == 0)
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

    var emails = await repository.ListEmailsAsync(ct);
    return Results.Ok(emails.Where(rollout.Allows).ToArray());
});

await InitialiseDatabaseAsync(app.Services, app.Lifetime.ApplicationStopping);
app.Run();

static async Task InitialiseDatabaseAsync(IServiceProvider services, CancellationToken ct)
{
    using var scope = services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<SubscriberRepository>().InitialiseAsync(ct);
}

static bool HasKey(HttpRequest request, string? expected, string header)
{
    return !string.IsNullOrWhiteSpace(expected)
        && request.Headers.TryGetValue(header, out var supplied)
        && supplied.Count == 1
        && string.Equals(supplied[0], expected, StringComparison.Ordinal);
}

static bool TryNormaliseEmail(string? input, out string email)
{
    email = input?.Trim().ToLowerInvariant() ?? "";
    return email.Length <= 254
        && MailAddress.TryCreate(email, out var address)
        && string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
}
