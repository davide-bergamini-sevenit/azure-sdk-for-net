// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

// <auto-generated/>

#nullable disable

using System.Text.Json;
using Azure.Core;

namespace Azure.ResourceManager.BillingBenefits.Models
{
    public partial class SavingsPlanValidateResult
    {
        internal static SavingsPlanValidateResult DeserializeSavingsPlanValidateResult(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Null)
            {
                return null;
            }
            Optional<bool> valid = default;
            Optional<string> reasonCode = default;
            Optional<string> reason = default;
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("valid"u8))
                {
                    if (property.Value.ValueKind == JsonValueKind.Null)
                    {
                        property.ThrowNonNullablePropertyIsNull();
                        continue;
                    }
                    valid = property.Value.GetBoolean();
                    continue;
                }
                if (property.NameEquals("reasonCode"u8))
                {
                    reasonCode = property.Value.GetString();
                    continue;
                }
                if (property.NameEquals("reason"u8))
                {
                    reason = property.Value.GetString();
                    continue;
                }
            }
            return new SavingsPlanValidateResult(Optional.ToNullable(valid), reasonCode.Value, reason.Value);
        }
    }
}
