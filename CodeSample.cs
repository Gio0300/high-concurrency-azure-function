using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class SalesOrderSubscriber
    {
        private readonly ILogger<SalesOrderSubscriber> _logger;

        public SalesOrderSubscriber(ILogger<SalesOrderSubscriber> logger)
        {
            _logger = logger;
        }

        [Function(nameof(SalesOrderSubscriber))]
        public async Task Run(
            [ServiceBusTrigger("sales_order", Connection = "sales_order_SERVICEBUS", IsSessionsEnabled = true, IsBatched = true)]
            ServiceBusReceivedMessage[] messages,
            ServiceBusMessageActions messageActions)
        {

            var PrimarySessionTask = PostOrderToTargetSystem(messages);

            var defCredentials = new DefaultAzureCredential();
            var SecondSessionTask = SubSessionProcessor(defCredentials);
            var ThirdSessionTask = SubSessionProcessor(defCredentials);
            var NthSessionTask = SubSessionProcessor(defCredentials);

            await Task.WhenAll(PrimarySessionTask, SecondSessionTask, ThirdSessionTask, NthSessionTask);
        }

        public async Task SubSessionProcessor(DefaultAzureCredential credentials)
        {
            await using var client = new ServiceBusClient(Environment.GetEnvironmentVariable("sales_order_fullyQualifiedNamespace"), credentials);
            ServiceBusSessionReceiver receiver = await client.AcceptNextSessionAsync("sales_order");
            ServiceBusReceivedMessage receivedMessage;
            List<ServiceBusReceivedMessage> messages = new List<ServiceBusReceivedMessage>();

            receivedMessage = await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(500));
            while (receivedMessage != null)
            {
                messages.Add(receivedMessage);
                receivedMessage = await receiver.ReceiveMessageAsync(TimeSpan.FromMilliseconds(500));
            }

            await PostOrderToTargetSystem(messages);

            foreach (var message in messages)
            {
                await receiver.CompleteMessageAsync(message);
            }
            await receiver.CloseAsync();

        }
        public async Task PostOrderToTargetSystem(IEnumerable<ServiceBusReceivedMessage> messages)
        {
            _logger.LogInformation("Order posted to target system");
        }

    }
}
