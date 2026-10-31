using System.Text.Json;
using RabbitMQ.Client;
using SubscriberService.Data;

namespace SubscriberService;

public sealed class SubscriberOutboxWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<SubscriberOutboxWorker> logger) : BackgroundService
{
    public const string QueueName = "happyheadlines.subscribers.created";
    public const string DeadLetterQueue = "happyheadlines.subscribers.failed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<SubscriberRepository>();
                var events = await repository.PendingEventsAsync(stoppingToken);

                if (events.Count > 0)
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = configuration["SubscriberRabbitMq:HostName"] ?? "localhost",
                        UserName = configuration["SubscriberRabbitMq:UserName"] ?? "guest",
                        Password = configuration["SubscriberRabbitMq:Password"] ?? "guest",
                        RequestedHeartbeat = TimeSpan.FromSeconds(30)
                    };

                    using var connection = factory.CreateConnection();
                    using var channel = connection.CreateModel();

                    channel.QueueDeclare(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
                    channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false,
                        arguments: new Dictionary<string, object>
                        {
                            ["x-dead-letter-exchange"] = "",
                            ["x-dead-letter-routing-key"] = DeadLetterQueue
                        });
                    channel.ConfirmSelect();

                    foreach (var subscriber in events)
                    {
                        stoppingToken.ThrowIfCancellationRequested();
                        var properties = channel.CreateBasicProperties();
                        properties.Persistent = true;
                        properties.ContentType = "application/json";
                        properties.MessageId = subscriber.Id.ToString();

                        channel.BasicPublish("", QueueName, false, properties,
                            JsonSerializer.SerializeToUtf8Bytes(subscriber));
                        channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(5));
                        await repository.MarkDispatchedAsync(subscriber.Id, stoppingToken);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Subscriber events could not be delivered. Retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
