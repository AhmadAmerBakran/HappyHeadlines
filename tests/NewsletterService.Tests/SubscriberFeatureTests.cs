using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using NewsletterService.Controllers;
using NewsletterService.Services;
using Xunit;

namespace NewsletterService.Tests;

public sealed class SubscriberFeatureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendChecksSubscriberRolloutBeforeReadingSubscribers(bool enabled)
    {
        var requests = new List<string>();
        using var subscriberHttp = new HttpClient(new TestHandler(request =>
        {
            requests.Add(request.RequestUri!.AbsolutePath);
            Assert.Equal("internal-test-key", request.Headers.GetValues("X-Subscriber-Internal-Key").Single());

            if (request.RequestUri.AbsolutePath == "/api/subscribers/rollout/status")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new { enabled })
                };
            }

            if (request.RequestUri.AbsolutePath == "/api/subscribers")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(Array.Empty<string>())
                };
            }

            throw new InvalidOperationException("Unexpected subscriber request.");
        }))
        {
            BaseAddress = new Uri("http://subscriber-service:8080/")
        };

        var configuration = CreateConfiguration();
        using var articleHttp = new HttpClient
        {
            BaseAddress = new Uri("http://article-service:8080/")
        };

        var controller = new NewslettersController(
            new NewsletterStore(),
            new ArticleClient(articleHttp),
            new SubscriberClient(subscriberHttp, configuration),
            new NewsletterMailer(configuration),
            configuration)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
        controller.Request.Headers["X-Newsletter-Admin-Key"] = "newsletter-test-key";

        var result = await controller.Send(
            new NewsletterSendRequest("Local headlines", "Daily news"),
            CancellationToken.None);

        if (enabled)
        {
            Assert.IsType<OkObjectResult>(result);
            Assert.Equal(
                new[] { "/api/subscribers/rollout/status", "/api/subscribers" },
                requests);
        }
        else
        {
            Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(new[] { "/api/subscribers/rollout/status" }, requests);
        }
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Subscriber:InternalKey"] = "internal-test-key",
                ["Newsletter:AdminKey"] = "newsletter-test-key"
            })
            .Build();

    private sealed class TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }
}
