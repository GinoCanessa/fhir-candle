// <copyright file="SubscriptionConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Common.Models;
using FhirStore.Models;

namespace FhirStore.Versioned.Shims.Subscriptions;

/// <summary>A subscription format converter.</summary>
public class SubscriptionConverter
{
    /// <summary>Attempts to parse a ParsedSubscription from the given object.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="common">      [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscription, out ParsedSubscription common)
    {
        if ((subscription == null) ||
            (subscription is not Hl7.Fhir.Model.Subscription sub) ||
            string.IsNullOrEmpty(sub.Id) ||
            string.IsNullOrEmpty(sub.Topic))
        {
            common = null!;
            return false;
        }

        common = new()
        {
            Id = sub.Id,
            TopicUrl = sub.Topic,
            ChannelSystem = sub.ChannelType?.System ?? string.Empty,
            ChannelCode = sub.ChannelType?.Code ?? string.Empty,
            Endpoint = sub.Endpoint,
            HeartbeatSeconds = sub.HeartbeatPeriod ?? 0,
            TimeoutSeconds = sub.Timeout ?? 0,
            ContentType = sub.ContentType,
            ContentLevel = sub.Content?.ToString() ?? string.Empty,
            MaxEventsPerNotification = sub.MaxCount ?? 0,
        };

        // add parameters
        if (sub.Parameter?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.Subscription.ParameterComponent param in sub.Parameter)
            {
                if (!common.Parameters.ContainsKey(param.Name))
                {
                    common.Parameters.Add(param.Name, new());
                }

                common.Parameters[param.Name].Add(param.Value.ToString());
            }
        }

        // add filters
        if (sub.FilterBy?.Any() ?? false)
        {
            foreach (Hl7.Fhir.Model.Subscription.FilterByComponent filter in sub.FilterBy)
            {
                string key = string.IsNullOrEmpty(filter.ResourceType) ? "*" : filter.ResourceType;

                if (!common.Filters.ContainsKey(key))
                {
                    common.Filters.Add(key, new());
                }

                common.Filters[key].Add(new(
                    filter.ResourceType ?? string.Empty,
                    filter.FilterParameter,
                    filter.Comparator?.ToString() ?? string.Empty,
                    filter.Modifier?.ToString() ?? string.Empty,
                    filter.Value));
            }
        }

        return true;
    }
}
