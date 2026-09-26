using System.Diagnostics;
using System.Text.Json;
using ArticleService.Data;
using ArticleService.Models;
using HappyHeadlines.Messaging;
using HappyHeadlines.Observability;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ArticleService.Messaging;

public sealed class PublishedArticleConsumer(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
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
                logger.LogWarning(ex, "Article queue connection failed. Retrying.");
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
        _channel.QueueDeclare(ArticleMessaging.ArticleServiceQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(ArticleMessaging.ArticleServiceQueue, ArticleMessaging.Exchange, string.Empty);
        _channel.BasicQos(0, 10, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleMessageAsync;
        _channel.BasicConsume(ArticleMessaging.ArticleServiceQueue, autoAck: false, consumer);

        logger.LogInformation("ArticleService is subscribed to published articles.");
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
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
            var message = JsonSerializer.Deserialize<PublishedArticle>(args.Body.Span)
                ?? throw new JsonException("Published article was empty.");

            if (!ArticleScopes.TryNormalize(message.Scope, out var scope))
            {
                logger.LogWarning(
                    "Discarding article {ArticleId} because scope {Scope} is invalid.",
                    message.Id,
                    message.Scope);
                _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
                return;
            }

            activity?.SetTag("article.id", message.Id);
            activity?.SetTag("article.scope", scope);

            var article = new Article
            {
                Id = message.Id,
                Title = message.Title,
                Content = message.Content,
                Source = message.Source,
                Scope = scope,
                CreatedAtUtc = message.PublishedAtUtc,
                UpdatedAtUtc = message.PublishedAtUtc
            };

            using var serviceScope = scopeFactory.CreateScope();
            var repository = serviceScope.ServiceProvider.GetRequiredService<IArticleRepository>();

            using var storageActivity = HappyHeadlinesDiagnostics.StartActivity("article store");
            var stored = await repository.StorePublishedAsync(article, CancellationToken.None);

            _channel!.BasicAck(args.DeliveryTag, multiple: false);

            logger.LogInformation(
                stored
                    ? "Stored published article {ArticleId}."
                    : "Published article {ArticleId} was already stored.",
                article.Id);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding an invalid published article message.");
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not store a published article.");
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
        }
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
