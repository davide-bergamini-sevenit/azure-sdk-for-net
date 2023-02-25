// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Azure.WebJobs.Extensions.ServiceBus.Config;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.ServiceBus;
using Microsoft.Azure.WebJobs.ServiceBus.Listeners;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.ServiceBus.Listeners
{
    internal class ServiceBusScalerProvider : IScalerProvider
    {
        private readonly IConfiguration _configuration;
        private AzureComponentFactory _defaultAzureComponentFactory;
        private readonly MessagingProvider _messagingProvider;
        private readonly IOptions<ServiceBusOptions> _options;
        private readonly AzureEventSourceLogForwarder _logForwarder;
        private readonly ITriggerMetadataProvider _triggerMetadataProvider;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IEnumerable<TriggerMetadata> _triggerMetadata;

        public ServiceBusScalerProvider(
            IConfiguration configuration,
            AzureComponentFactory defaultAzureComponentFactory,
            ITriggerMetadataProvider triggerMetadataProvider,
            MessagingProvider messagingProvider,
            AzureEventSourceLogForwarder logForwarder,
            IOptions<ServiceBusOptions> options,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _defaultAzureComponentFactory = defaultAzureComponentFactory;
            _triggerMetadataProvider = triggerMetadataProvider;
            _triggerMetadata = triggerMetadataProvider.GetTriggerMetadata();
            _messagingProvider = messagingProvider;
            _logForwarder = logForwarder;
            _options = options;
            _loggerFactory = loggerFactory;
        }

        public IEnumerable<IScaleMonitor> GetScaleMonitors()
        {
            foreach (var triggerMetada in _triggerMetadata.Where(x => x.Type == "serviceBusTrigger"))
            {
                yield return GetMonitor(triggerMetada);
            }
        }

        public IEnumerable<ITargetScaler> GetTargetScalers()
        {
            foreach (var triggerMetada in _triggerMetadata.Where(x => x.Type == "serviceBusTrigger"))
            {
                yield return GetTargetScaler(triggerMetada);
            }
        }

        private IScaleMonitor GetMonitor(TriggerMetadata triggerMetadata)
        {
            ServiceBusMetadata serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadata>(triggerMetadata.Metadata.ToString());
            AzureComponentFactory azureComponentFactory = GetAzureComponentFactory(triggerMetadata);

            (ServiceBusAdministrationClient adminClient, ServiceBusReceiver receiver) = CreateParameters(_configuration, serviceBusMetadata, azureComponentFactory);

            return new ServiceBusScaleMonitor(
                triggerMetadata.FunctionName,
                receiver.EntityPath,
                !string.IsNullOrEmpty(serviceBusMetadata.QueueName) ? ServiceBusEntityType.Queue : ServiceBusEntityType.Topic,
                new Lazy<ServiceBusReceiver>(() => receiver),
                new Lazy<ServiceBusAdministrationClient>(() => adminClient),
                _loggerFactory);
        }

        private ITargetScaler GetTargetScaler(TriggerMetadata triggerMetadata)
        {
            ServiceBusMetadata serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadata>(triggerMetadata.Metadata.ToString());
            AzureComponentFactory azureComponentFactory = GetAzureComponentFactory(triggerMetadata);

            (ServiceBusAdministrationClient adminClient, ServiceBusReceiver receiver) = CreateParameters(_configuration, serviceBusMetadata, azureComponentFactory);

            return new ServiceBusTargetScaler(
                triggerMetadata.FunctionName,
                receiver.EntityPath,
                receiver.EntityPath.Contains("//") ? ServiceBusEntityType.Topic : ServiceBusEntityType.Queue,
                new Lazy<ServiceBusReceiver>(() => receiver),
                new Lazy<ServiceBusAdministrationClient>(() => adminClient),
                _options.Value,
                serviceBusMetadata.IsSessionsEnabled,
                !string.Equals(serviceBusMetadata.Cardinality, "many"),
                _loggerFactory);
        }

        private (ServiceBusAdministrationClient AdminClient, ServiceBusReceiver Receiver) CreateParameters(IConfiguration configuration, ServiceBusMetadata serviceBusMetadata, AzureComponentFactory azureComponentFactory)
        {
            ServiceBusClientFactory factory = new ServiceBusClientFactory(configuration, azureComponentFactory, _messagingProvider, _logForwarder, _options);

            ServiceBusAdministrationClient adminClient = factory.CreateAdministrationClient(serviceBusMetadata.Connection);
            ServiceBusClient client = factory.CreateClientFromSetting(serviceBusMetadata.Connection);
            ServiceBusReceiver receiver = !string.IsNullOrEmpty(serviceBusMetadata.QueueName) ? client.CreateReceiver(serviceBusMetadata.QueueName)
                : client.CreateReceiver(serviceBusMetadata.TopicName, serviceBusMetadata.SubscriptionName);

            return (AdminClient: adminClient, Receiver: receiver);
        }

        private AzureComponentFactory GetAzureComponentFactory(TriggerMetadata triggerMetadata)
        {
            AzureComponentFactory azureComponentFactory = null;
            if (triggerMetadata.Properties.TryGetValue(nameof(AzureComponentFactory), out object factoryObject))
            {
                azureComponentFactory = factoryObject as AzureComponentFactory;
            }
            return azureComponentFactory ?? _defaultAzureComponentFactory;
        }

        internal class ServiceBusMetadata
        {
            [JsonProperty]
            public string Type { get; set; }

            [JsonProperty]
            public string Connection { get; set; }

            [JsonProperty]
            public string QueueName { get; set; }

            [JsonProperty]
            public string TopicName { get; set; }

            [JsonProperty]
            public string SubscriptionName { get; set; }

            [JsonProperty]
            public bool IsSessionsEnabled { get; set; }

            [JsonProperty]
            public string Cardinality { get; set; }
        }
    }
}
