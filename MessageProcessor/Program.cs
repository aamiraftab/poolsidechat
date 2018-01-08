using System;
using System.Configuration;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.ServiceBus.Messaging;

namespace MessageProcessor
{
    class Program
    {
        private static void Main()
        {
            var eventHubConnectionString = ConfigurationManager.AppSettings["eventHubConnectionString"];
            var eventHubName = ConfigurationManager.AppSettings["sourceEventHubName"];
            var storageAccountName = ConfigurationManager.AppSettings["storageAccountName"];
            var storageAccountKey = ConfigurationManager.AppSettings["storageAccountKey"];

            var storageConnectionString =
                $"DefaultEndpointsProtocol=https;AccountName={storageAccountName};AccountKey={storageAccountKey}";

            var eventProcessorHostName = Guid.NewGuid().ToString();
            var eventProcessorHost = new EventProcessorHost(eventProcessorHostName, eventHubName,
                EventHubConsumerGroup.DefaultGroupName, eventHubConnectionString, storageConnectionString);

            var eventHubConfig = new EventHubConfiguration();
            eventHubConfig.AddEventProcessorHost(eventHubName, eventProcessorHost);

            var config = new JobHostConfiguration(storageConnectionString);
            config.UseEventHub(eventHubConfig);
            
            Console.WriteLine("Registering EventProcessor...");

            var options = new EventProcessorOptions();
            options.ExceptionReceived += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.Exception);
                Console.ResetColor();
            };

            eventProcessorHost.RegisterEventProcessorAsync<MessageProcessor>(options);

            var host = new JobHost(config);
            host.RunAndBlock();

            eventProcessorHost.UnregisterEventProcessorAsync().Wait();
        }
    }
}
