using System.Diagnostics;
using System.Text.Json;
using HappyHeadlines.Messaging;
using HappyHeadlines.Observability;
using RabbitMQ.Client;

namespace PublisherService.Services;

public sealed class ArticlePublisher(
    IConfiguration configuration,
    ILogger<ArticlePublisher> logger) : IDisposable
{
    private readonly object _connectionLock = new();
    private IConnection? _connection;

    public Task PublishAsync(PublishedArticle article, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var activity = HappyHeadlinesDiagnostics.StartActivity(
            "articles.published send",
            ActivityKind.Producer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", ArticleMessaging.Exchange);
        activity?.SetTag("messaging.operation", "publish");
        activity?.SetTag("article.id", article.Id);
        activity?.SetTag("article.scope", article.Scope);

        var connection = GetConnection();
        using var channel = connection.CreateModel();

        channel.ExchangeDeclare(
            exchange: ArticleMessaging.Exchange,
            type: ExchangeType.Fanout,
            durable: true,
            autoDelete: false);

        DeclareSubscription(channel, ArticleMessaging.ArticleServiceQueue);
        DeclareSubscription(channel, ArticleMessaging.NewsletterServiceQueue);

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = nameof(PublishedArticle);
        properties.MessageId = article.Id.ToString();
        RabbitMqTraceContext.Inject(properties);

        var body = JsonSerializer.SerializeToUtf8Bytes(article);
        channel.BasicPublish(
            exchange: ArticleMessaging.Exchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: body);

        logger.LogInformation(
            "Published article {ArticleId} for {Scope}.",
            article.Id,
            article.Scope);

        return Task.CompletedTask;
    }

    private static void DeclareSubscription(IModel channel, string queue)
    {
        channel.QueueDeclare(
            queue: queue,
            durable: true,
            exclusive: false,
            autoDelete: false);
        channel.QueueBind(queue, ArticleMessaging.Exchange, string.Empty);
    }

    private IConnection GetConnection()
    {
        lock (_connectionLock)
        {
            if (_connection is { IsOpen: true })
            {
                return _connection;
            }

            _connection?.Dispose();

            var factory = new ConnectionFactory
            {
                HostName = configuration["RabbitMq:HostName"] ?? "localhost",
                UserName = configuration["RabbitMq:UserName"] ?? "guest",
                Password = configuration["RabbitMq:Password"] ?? "guest",
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            _connection = factory.CreateConnection();
            return _connection;
        }
    }

    public void Dispose()
    {
        lock (_connectionLock)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
