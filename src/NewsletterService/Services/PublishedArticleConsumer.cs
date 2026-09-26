using System.Diagnostics;
using System.Text.Json;
using HappyHeadlines.Messaging;
using HappyHeadlines.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NewsletterService.Services;

public sealed class PublishedArticleConsumer(
    IConfiguration configuration,
    NewsletterStore store,
    ILogger<PublishedArticleConsumer> logger) : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                StartConsumer();
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Newsletter queue connection failed. Retrying.");
                DisposeConnection();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void StartConsumer()
    {
        var factory = CreateFactory();
        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        _channel.ExchangeDeclare(ArticleMessaging.Exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        _channel.QueueDeclare(ArticleMessaging.NewsletterServiceQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(ArticleMessaging.NewsletterServiceQueue, ArticleMessaging.Exchange, string.Empty);
        _channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleMessageAsync;
        _channel.BasicConsume(ArticleMessaging.NewsletterServiceQueue, autoAck: false, consumer);

        logger.LogInformation("NewsletterService is subscribed to published articles.");
    }

    private Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        var parentContext = RabbitMqTraceContext.Extract(args.BasicProperties);
        using var activity = parentContext.TraceId != default
            ? HappyHeadlinesDiagnostics.StartActivity(
                "articles.published receive",
                ActivityKind.Consumer,
                parentContext)
            : HappyHeadlinesDiagnostics.StartActivity(
                "articles.published receive",
                ActivityKind.Consumer);

        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", ArticleMessaging.Exchange);
        activity?.SetTag("messaging.operation", "process");

        try
        {
            var article = JsonSerializer.Deserialize<PublishedArticle>(args.Body.Span)
                ?? throw new JsonException("Published article was empty.");

            activity?.SetTag("article.id", article.Id);
            activity?.SetTag("article.scope", article.Scope);

            store.Add(article);
            _channel!.BasicAck(args.DeliveryTag, multiple: false);

            logger.LogInformation(
                "Received published article {ArticleId} for the newsletter.",
                article.Id);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding an invalid published article message.");
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not process a published article message.");
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
        }

        return Task.CompletedTask;
    }

    private ConnectionFactory CreateFactory()
    {
        return new ConnectionFactory
        {
            HostName = configuration["RabbitMq:HostName"] ?? "localhost",
            UserName = configuration["RabbitMq:UserName"] ?? "guest",
            Password = configuration["RabbitMq:Password"] ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };
    }

    private void DisposeConnection()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }

    public override void Dispose()
    {
        DisposeConnection();
        base.Dispose();
    }
}
