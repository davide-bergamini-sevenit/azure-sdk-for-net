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
    internal class ServiceBusScalerProvider : IScaleMonitorProvider, ITargetScalerProvider
    {
        private readonly IConfiguration _configuration;
        private AzureComponentFactory _defaultAzureComponentFactory;
        private readonly MessagingProvider _messagingProvider;
        private readonly IOptions<ServiceBusOptions> _options;
        private readonly AzureEventSourceLogForwarder _logForwarder;
        private readonly TriggerMetadata _triggerMetadata;
        private readonly ILoggerFactory _loggerFactory;

        public ServiceBusScalerProvider(
            IConfiguration configuration,
            AzureComponentFactory defaultAzureComponentFactory,
            TriggerMetadata triggerMetadata,
            MessagingProvider messagingProvider,
            AzureEventSourceLogForwarder logForwarder,
            IOptions<ServiceBusOptions> options,
            ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _defaultAzureComponentFactory = defaultAzureComponentFactory;
            _triggerMetadata = triggerMetadata;
            _messagingProvider = messagingProvider;
            _logForwarder = logForwarder;
            _options = options;
            _loggerFactory = loggerFactory;
        }

        public IScaleMonitor GetMonitor()
        {
            ServiceBusMetadata serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadata>(_triggerMetadata.Metadata.ToString());
            AzureComponentFactory azureComponentFactory = GetAzureComponentFactory(_triggerMetadata);

            (ServiceBusAdministrationClient adminClient, ServiceBusReceiver receiver) = CreateParameters(_configuration, serviceBusMetadata, azureComponentFactory);

            return new ServiceBusScaleMonitor(
                _triggerMetadata.FunctionName,
                receiver.EntityPath,
                !string.IsNullOrEmpty(serviceBusMetadata.QueueName) ? ServiceBusEntityType.Queue : ServiceBusEntityType.Topic,
                new Lazy<ServiceBusReceiver>(() => receiver),
                new Lazy<ServiceBusAdministrationClient>(() => adminClient),
                _loggerFactory);
        }

        public ITargetScaler GetTargetScaler()
        {
            ServiceBusMetadata serviceBusMetadata = JsonConvert.DeserializeObject<ServiceBusMetadata>(_triggerMetadata.Metadata.ToString());
            AzureComponentFactory azureComponentFactory = GetAzureComponentFactory(_triggerMetadata);

            (ServiceBusAdministrationClient adminClient, ServiceBusReceiver receiver) = CreateParameters(_configuration, serviceBusMetadata, azureComponentFactory);

            return new ServiceBusTargetScaler(
                _triggerMetadata.FunctionName,
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
