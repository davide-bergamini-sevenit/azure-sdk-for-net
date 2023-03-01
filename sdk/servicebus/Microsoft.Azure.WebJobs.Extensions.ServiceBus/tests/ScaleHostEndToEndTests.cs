// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus.Tests;
using Castle.Components.DictionaryAdapter.Xml;
using Microsoft.Azure.WebJobs.Host.EndToEndTests;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.ServiceBus.Tests
{
    internal class ScaleHostEndToEndTests : WebJobsServiceBusTestBase
    {
        private const string Function1Name = "Function1";
        private const string Function2Name = "Function2";

        public ScaleHostEndToEndTests() : base(isSession: false)
        {
        }

        [Test]
        [TestCase(false)]
        [TestCase(true)]
        public async Task ScaleHostEndToEndTest(bool tbsEnabled)
        {
            string hostJson =
            @"{
                ""azureWebJobs"" : {
                    ""extensions"": {
                        ""serviceBus"": {
                            ""maxConcurrentCalls"" :  1
                        }
                    }
                }
            }";

            // Function1Name uses conection string
            // Function2Name uses AzureCompoentFactory - simulating managed identity scenario in ScaleController
            string triggers = $@"{{
""triggers"": [
    {{
        ""name"": ""myQueueItem"",
        ""type"": ""serviceBusTrigger"",
        ""direction"": ""in"",
        ""queueName"": ""{FirstQueueScope.QueueName}"",
        ""functionName"": ""{Function1Name}""
    }},
    {{
        ""name"": ""myQueueItem"",
        ""type"": ""serviceBusTrigger"",
        ""direction"": ""in"",
        ""queueName"": ""{SecondQueueScope.QueueName}"",
        ""connection"": ""ServiceBusConnection2"",
        ""functionName"": ""{Function2Name}""
    }}
 ]}}";

            IHost host = new HostBuilder().ConfigureServices(services => services.AddAzureClientsCore()).Build();
            AzureComponentFactory defaultAzureComponentFactory = host.Services.GetService<AzureComponentFactory>();
            AzureComponentFactoryWrapper factoryWrapper = new AzureComponentFactoryWrapper(defaultAzureComponentFactory, ServiceBusTestEnvironment.Instance.Credential);
            TriggerMetadataProvider triggerMetadataProvider = new TriggerMetadataProvider(new Dictionary<string, AzureComponentFactory>() { { Function2Name, factoryWrapper } }, triggers);

            string hostId = "test-host";
            var loggerProvider = new TestLoggerProvider();

            IHostBuilder hostBuilder = new HostBuilder();
            hostBuilder.ConfigureLogging(configure =>
            {
                configure.SetMinimumLevel(LogLevel.Debug);
                configure.AddProvider(loggerProvider);
            });
            hostBuilder.ConfigureAppConfiguration((hostBuilderContext, config) =>
            {
                // Adding host.json here
                config.AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(hostJson)));

                var settings = new Dictionary<string, string>();
                settings.Add("AzureWebJobsServiceBus", ServiceBusTestEnvironment.Instance.ServiceBusConnectionString);
                settings.Add("ServiceBusConnection2:fullyQualifiedNamespace", $"{SecondQueueScope.NamespaceName}.servicebus.windows.net");
                settings.Add("FeatureManagement:Microsoft.Azure.WebJobs.Extensions.ServiceBus", "true");

                // Adding app setting
                config.AddInMemoryCollection(settings);
            })
            .ConfigureServices(services =>
            {
                services.AddAzureClientsCore();
                services.AddAzureStorageScaleServices();

                services.AddSingleton<IScaleMetricsRepository, TestScaleMetricsRepository>();
            })
            .ConfigureWebJobsScale((context, builder) =>
            {
                builder.UseHostId(hostId);

                foreach (var jtocken in JObject.Parse(triggers)["triggers"])
                {
                    TriggerMetadata metadata = new TriggerMetadata(jtocken as JObject);
                    if (metadata.FunctionName == Function1Name)
                    {
                        metadata.Properties[nameof(AzureComponentFactory)] = defaultAzureComponentFactory;
                    }
                    else
                    {
                        metadata.Properties[nameof(AzureComponentFactory)] = factoryWrapper;
                    }
                    builder.AddServiceBusScaleForTrigger(metadata);
                }
            },
            scaleOptions =>
            {
                scaleOptions.IsTargetScalingEnabled = tbsEnabled;
                scaleOptions.MetricsPurgeEnabled = false;
                scaleOptions.ScaleMetricsMaxAge = TimeSpan.FromMinutes(4);
            });

            IHost scaleHost = hostBuilder.Build();
            await scaleHost.StartAsync();

            // add some messages to the queue
            await WriteQueueMessage("test");
            await WriteQueueMessage("test");
            await WriteQueueMessage("test", queueName: SecondQueueScope.QueueName);
            await WriteQueueMessage("test", queueName: SecondQueueScope.QueueName);
            await WriteQueueMessage("test", queueName: SecondQueueScope.QueueName);

            await TestHelpers.Await(async () =>
            {
                IScaleManager scaleManager = scaleHost.Services.GetService<IScaleManager>();

                var scaleStatus = await scaleManager.GetScaleStatusAsync(new ScaleStatusContext());
                var functionScaleStatuses = scaleStatus.FunctionScaleStatuses;

                bool scaledOut = false;
                if (!tbsEnabled)
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == null
                     && functionScaleStatuses[Function1Name].Vote == ScaleVote.ScaleOut
                     && functionScaleStatuses[Function2Name].Vote == ScaleVote.ScaleOut;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains("2 scale monitors to sample", logMessages);
                    }
                }
                else
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == 3
                     && functionScaleStatuses[Function1Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function1Name].TargetWorkerCount == 2
                     && functionScaleStatuses[Function2Name].Vote == ScaleVote.ScaleOut && functionScaleStatuses[Function2Name].TargetWorkerCount == 3;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains("2 target scalers to sample", logMessages);
                    }
                }

                if (scaledOut)
                {
                    var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                    Assert.Contains("Scale monitor service is started.", logMessages);
                    Assert.Contains("Scaling out based on votes", logMessages);
                }

                return scaledOut;
            });
        }

        internal class TriggerMetadataProvider : ITriggerMetadataProvider
        {
            private readonly Dictionary<string, AzureComponentFactory> _factories;

            private readonly string _triggers;

            public TriggerMetadataProvider(Dictionary<string, AzureComponentFactory> factories, string triggers)
            {
                _factories = factories;
                _triggers = triggers;
            }

            public IEnumerable<TriggerMetadata> GetTriggerMetadata()
            {
                JObject triggers = JObject.Parse(_triggers);
                foreach (var jt in triggers["triggers"])
                {
                    JObject jo = jt.ToObject<JObject>();
                    var triggerMetadata = new TriggerMetadata(jo);
                    if (_factories.TryGetValue(triggerMetadata.FunctionName, out AzureComponentFactory triggerFactory))
                    {
                        triggerMetadata.Properties[nameof(AzureComponentFactory)] = triggerFactory;
                    }
                    yield return triggerMetadata;
                }
            }
        }

        internal class TestScaleMetricsRepository : IScaleMetricsRepository
        {
            private Dictionary<IScaleMonitor, List<ScaleMetrics>> _cache;

            public TestScaleMetricsRepository()
            {
                _cache = new Dictionary<IScaleMonitor, List<ScaleMetrics>>();
            }

            public Task WriteMetricsAsync(IDictionary<IScaleMonitor, ScaleMetrics> monitorMetrics)
            {
                foreach (var kvp in monitorMetrics)
                {
                    if (_cache.TryGetValue(kvp.Key, out var value))
                    {
                        value.Add(kvp.Value);
                    }
                    else
                    {
                        _cache[kvp.Key] = new List<ScaleMetrics> { kvp.Value };
                    }
                }

                return Task.CompletedTask;
            }

            public Task<IDictionary<IScaleMonitor, IList<ScaleMetrics>>> ReadMetricsAsync(IEnumerable<IScaleMonitor> monitors)
            {
                IDictionary<IScaleMonitor, IList<ScaleMetrics>> result = new Dictionary<IScaleMonitor, IList<ScaleMetrics>>();

                foreach (var monitor in monitors)
                {
                    if (_cache.TryGetValue(monitor, out var value))
                    {
                        result.Add(monitor, value);
                    }
                    else
                    {
                        result.Add(monitor, new List<ScaleMetrics>());
                    }
                }

                return Task.FromResult(result);
            }
        }

        internal class AzureComponentFactoryWrapper : AzureComponentFactory
        {
            private readonly AzureComponentFactory _factory;
            private readonly TokenCredential _tokenCredential;

            public AzureComponentFactoryWrapper(AzureComponentFactory factory, TokenCredential tokenCredential)
            {
                _factory = factory;
                _tokenCredential = tokenCredential;
            }

            public override TokenCredential CreateTokenCredential(IConfiguration configuration)
            {
                return _tokenCredential != null ? _tokenCredential : _factory.CreateTokenCredential(configuration);
            }

            public override object CreateClientOptions(Type optionsType, object serviceVersion, IConfiguration configuration)
                => _factory.CreateClientOptions(optionsType, serviceVersion, configuration);

            public override object CreateClient(Type clientType, IConfiguration configuration, TokenCredential credential, object clientOptions)
                => _factory.CreateClient(clientType, configuration, credential, clientOptions);
        }
    }
}
