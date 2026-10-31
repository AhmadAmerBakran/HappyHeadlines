using System.Net.Mail;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NewsletterService.Services;

public sealed record SubscriberCreated(Guid Id, string Email, DateTime SubscribedAtUtc);

public sealed class SubscriberWelcomeConsumer(
    IConfiguration configuration,
    NewsletterMailer mailer,
    ILogger<SubscriberWelcomeConsumer> logger) : BackgroundService
{
    private const string Queue = "happyheadlines.subscribers.created";
    private const string DeadLetterQueue = "happyheadlines.subscribers.failed";
    private IConnection? _connection;
    private IModel? _channel;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Connect();
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Subscriber queue is unavailable. Retrying.");
                CloseConnection();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void Connect()
    {
        var factory = new ConnectionFactory
        {
            HostName = configuration["SubscriberRabbitMq:HostName"] ?? "localhost",
            UserName = configuration["SubscriberRabbitMq:UserName"] ?? "guest",
            Password = configuration["SubscriberRabbitMq:Password"] ?? "guest",
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.QueueDeclare(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object>
            {
                ["x-dead-letter-exchange"] = "",
                ["x-dead-letter-routing-key"] = DeadLetterQueue
            });
        _channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleAsync;
        _channel.BasicConsume(Queue, autoAck: false, consumer);
        logger.LogInformation("Listening for new subscribers.");
    }

    private async Task HandleAsync(object sender, BasicDeliverEventArgs delivery)
    {
        try
        {
            var subscriber = JsonSerializer.Deserialize<SubscriberCreated>(delivery.Body.Span)
                ?? throw new JsonException("Subscriber event is empty.");

            if (subscriber.Id == Guid.Empty || !MailAddress.TryCreate(subscriber.Email, out _))
                throw new JsonException("Invalid subscriber event.");

            await mailer.SendAsync(subscriber.Email, "Welcome to Happy Headlines",
                "Thanks for subscribing to Happy Headlines.");
            _channel!.BasicAck(delivery.DeliveryTag, false);
            logger.LogInformation("Welcome email sent to {Email}.", subscriber.Email);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding an invalid subscriber event.");
            _channel!.BasicNack(delivery.DeliveryTag, false, false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Welcome email failed; message moved to the failed queue.");
            _channel!.BasicNack(delivery.DeliveryTag, false, false);
        }
    }

    private void CloseConnection()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        _channel = null;
        _connection = null;
    }

    public override void Dispose()
    {
        CloseConnection();
        base.Dispose();
    }
}
