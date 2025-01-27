// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System;
using System.Text.Json;
using Azure.Core;

namespace Azure.ResourceManager.ContainerRegistry.Models
{
    public partial class ContainerRegistryWebhookEvent
    {
        internal static ContainerRegistryWebhookEvent DeserializeContainerRegistryWebhookEvent(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            Optional<ContainerRegistryWebhookEventRequestMessage> eventRequestMessage = default;
            Optional<ContainerRegistryWebhookEventResponseMessage> eventResponseMessage = default;
            Optional<Guid> id = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("eventRequestMessage"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        property.ThrowNonNullablePropertyIsNull();
                        continue;
                    }
                    eventRequestMessage = ContainerRegistryWebhookEventRequestMessage.DeserializeContainerRegistryWebhookEventRequestMessage(property.Value);
                    continue;
                }
                if (property.NameEquals("eventResponseMessage"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        property.ThrowNonNullablePropertyIsNull();
                        continue;
                    }
                    eventResponseMessage = ContainerRegistryWebhookEventResponseMessage.DeserializeContainerRegistryWebhookEventResponseMessage(property.Value);
                    continue;
                }
                if (property.NameEquals("id"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        property.ThrowNonNullablePropertyIsNull();
                        continue;
                    }
                    id = property.Value.GetGuid();
                    continue;
                }
            }
            return new ContainerRegistryWebhookEvent(Optional.ToNullable(id), eventRequestMessage.Value, eventResponseMessage.Value);
        }
    }
}
