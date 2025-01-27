// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Collections.Generic;
using System.Text.Json;
using Azure.Core;

namespace Azure.ResourceManager.Network.Models
{
    public partial class VirtualWanSecurityProviders
    {
        internal static VirtualWanSecurityProviders DeserializeVirtualWanSecurityProviders(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            Optional<IReadOnlyList<VirtualWanSecurityProvider>> supportedProviders = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("supportedProviders"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        property.ThrowNonNullablePropertyIsNull();
                        continue;
                    }
                    List<VirtualWanSecurityProvider> array = new List<VirtualWanSecurityProvider>();
                    foreach (var item in property.Value.EnumerateArray())
                    {
                        array.Add(VirtualWanSecurityProvider.DeserializeVirtualWanSecurityProvider(item));
                    }
                    supportedProviders = array;
                    continue;
                }
            }
            return new VirtualWanSecurityProviders(Optional.ToList(supportedProviders));
        }
    }
}
