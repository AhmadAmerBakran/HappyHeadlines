using System.Net.Http.Json;
using HappyHeadlines.Observability;

const string page = """
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <title>Happy Headlines - Publish</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 720px; margin: 40px auto; padding: 0 20px; }
        label { display: block; margin-top: 16px; }
        input, textarea, select { width: 100%; padding: 8px; box-sizing: border-box; }
        textarea { min-height: 180px; }
        button { margin-top: 18px; padding: 10px 18px; }
    </style>
</head>
<body>
    <h1>Publish an article</h1>
    <form method="post" action="/publish">
        <label>Title</label>
        <input name="title" required>
        <label>Content</label>
        <textarea name="content" required></textarea>
        <label>Source</label>
        <input name="source" value="Happy Headlines">
        <label>Scope</label>
        <select name="scope">
            <option value="global">Global</option>
            <option value="europe">Europe</option>
            <option value="africa">Africa</option>
            <option value="asia">Asia</option>
            <option value="australia">Australia</option>
            <option value="north-america">North America</option>
            <option value="south-america">South America</option>
            <option value="antarctica">Antarctica</option>
        </select>
        <button type="submit">Publish</button>
    </form>
</body>
</html>
""";

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("WebApp");
builder.Services.AddHttpClient("publisher", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:PublisherService"] ?? "http://localhost:8083/");
});

var app = builder.Build();

app.UseHappyHeadlinesRequestLogging();

app.MapGet("/", () => Results.Content(page, "text/html"));

app.MapPost("/publish", async (
    HttpRequest request,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var form = await request.ReadFormAsync(cancellationToken);
    var payload = new
    {
        title = form["title"].ToString(),
        content = form["content"].ToString(),
        source = form["source"].ToString(),
        scope = form["scope"].ToString()
    };

    var client = httpClientFactory.CreateClient("publisher");
    using var response = await client.PostAsJsonAsync(
        "api/publishing",
        payload,
        cancellationToken);

    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(
        body,
        "application/json",
        statusCode: (int)response.StatusCode);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    instance = Environment.MachineName
}));

app.Run();
