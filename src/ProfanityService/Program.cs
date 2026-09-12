using ProfanityService.Data;
using ProfanityService.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddScoped<IProfanityRepository, ProfanityRepository>();
builder.Services.AddScoped<IProfanityFilter, ProfanityFilter>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-ProfanityService-Instance"] = Environment.MachineName;
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
