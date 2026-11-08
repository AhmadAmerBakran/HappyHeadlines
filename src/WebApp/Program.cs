using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.ResponseCompression;
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
    <p><a href="/headlines">Browse headlines</a></p>
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

const string headlinesPage = """
<!doctype html>
<html lang="en">
<head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>Happy Headlines - Headlines</title>
    <style>
        body { font-family: Arial, sans-serif; max-width: 720px; margin: 40px auto; padding: 0 20px; }
        #headlines button { display: block; width: 100%; padding: 12px; margin: 8px 0; text-align: left; cursor: pointer; }
        #articleContent { white-space: pre-wrap; line-height: 1.5; }
    </style>
</head>
<body>
    <p><a href="/">Publish an article</a></p>
    <h1>Latest headlines</h1>
    <p id="status">Loading headlines...</p>
    <div id="headlines"></div>
    <article>
        <h2 id="articleTitle"></h2>
        <p id="articleContent"></p>
    </article>
    <script>
        async function showArticle(id) {
            const status = document.getElementById('status');
            status.textContent = 'Loading article...';

            try {
                const response = await fetch('/api/headlines/' + encodeURIComponent(id));
                if (!response.ok) throw new Error('Article unavailable');

                const article = await response.json();
                document.getElementById('articleTitle').textContent = article.title;
                document.getElementById('articleContent').textContent = article.content;
                status.textContent = '';
            } catch {
                status.textContent = 'Could not load this article.';
            }
        }

        async function loadHeadlines() {
            const status = document.getElementById('status');

            try {
                const response = await fetch('/api/headlines');
                if (!response.ok) throw new Error('Headlines unavailable');

                const headlines = await response.json();
                const container = document.getElementById('headlines');

                for (const headline of headlines) {
                    const button = document.createElement('button');
                    button.type = 'button';
                    button.textContent = headline.title;
                    button.addEventListener('click', () => showArticle(headline.id));
                    container.appendChild(button);
                }

                status.textContent = headlines.length === 0 ? 'No published headlines yet.' : '';
            } catch {
                status.textContent = 'Could not load headlines.';
            }
        }

        loadHeadlines();
    </script>
</body>
</html>
""";

var builder = WebApplication.CreateBuilder(args);

builder.AddHappyHeadlinesObservability("WebApp");
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);

builder.Services.AddHttpClient("articles", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ArticleService"] ?? "http://localhost:8080/");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip
});
builder.Services.AddHttpClient("publisher", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:PublisherService"] ?? "http://localhost:8083/");
});

var app = builder.Build();

app.UseResponseCompression();
app.UseHappyHeadlinesRequestLogging();

app.MapGet("/", () => Results.Content(page, "text/html"));
app.MapGet("/headlines", () => Results.Content(headlinesPage, "text/html"));

app.MapGet("/api/headlines", async (
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var client = httpClientFactory.CreateClient("articles");
    using var response = await client.GetAsync("api/articles/global/headlines?limit=20", cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
});

app.MapGet("/api/headlines/{id:guid}", async (
    Guid id,
    IHttpClientFactory httpClientFactory,
    CancellationToken cancellationToken) =>
{
    var client = httpClientFactory.CreateClient("articles");
    using var response = await client.GetAsync($"api/articles/global/{id}", cancellationToken);
    var body = await response.Content.ReadAsStringAsync(cancellationToken);
    return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
});

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
