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
    /// <summary>(Immutable) URL of the filter criteria.</summary>
    private const string _filterCriteriaUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-filter-criteria";

    /// <summary>(Immutable) URL of the heartbeat period.</summary>
    private const string _heartbeatPeriodUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-heartbeat-period";

    /// <summary>(Immutable) URL of the timeout.</summary>
    private const string _timeoutUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-timeout";

    /// <summary>(Immutable) URL of the maximum count.</summary>
    private const string _maxCountUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-max-count";

    /// <summary>(Immutable) URL of the channel type.</summary>
    private const string _channelTypeUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-channel-type";

    /// <summary>(Immutable) URL of the content.</summary>
    private const string _contentUrl = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/backport-payload-content";

    /// <summary>Attempts to parse a ParsedSubscription from the given object.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <param name="common">      [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscription, out ParsedSubscription common)
    {
        if ((subscription == null) ||
            (subscription is not Hl7.Fhir.Model.Subscription sub) ||
            string.IsNullOrEmpty(sub.Id) ||
            string.IsNullOrEmpty(sub.Criteria))
        {
            common = null!;
            return false;
        }

        common = new()
        {
            Id = sub.Id,
            TopicUrl = sub.Criteria,
            ChannelSystem = string.Empty,
            ChannelCode = sub.Channel.Type.ToString()!,
            Endpoint = sub.Channel.Endpoint ?? string.Empty,
            ContentType = sub.Channel.Payload?.ToString() ?? string.Empty,
        };

        // check for extended information
        IEnumerable<Hl7.Fhir.Model.Extension>? exts;

        exts = sub.Channel.Extension?.Where(e => e.Url.Equals(_heartbeatPeriodUrl));
        if (exts?.Any() ?? false)
        {
            if (int.TryParse(exts.First().Value.ToString(), out int heartbeat))
            {
                common.HeartbeatSeconds = heartbeat;
            }
        }

        exts = sub.Channel.Extension?.Where(e => e.Url.Equals(_timeoutUrl));
        if (exts?.Any() ?? false)
        {
            if (int.TryParse(exts.First().Value.ToString(), out int timeout))
            {
                common.TimeoutSeconds = timeout;
            }
        }

        exts = sub.Channel.Extension?.Where(e => e.Url.Equals(_maxCountUrl));
        if (exts?.Any() ?? false)
        {
            if (int.TryParse(exts.First().Value.ToString(), out int maxCount))
            {
                common.MaxEventsPerNotification = maxCount;
            }
        }

        exts = sub.Channel.TypeElement?.Extension?.Where(e => e.Url.Equals(_channelTypeUrl));
        if (exts?.Any() ?? false)
        {
            Hl7.Fhir.Model.Coding c = (Hl7.Fhir.Model.Coding)exts.First().Value;
            common.ChannelSystem = c.System;
            common.ChannelCode = c.Code;
        }

        exts = sub.Channel.PayloadElement?.Extension?.Where(e => e.Url.Equals(_contentUrl));
        if (exts?.Any() ?? false)
        {
            common.ContentLevel = exts.First().Value.ToString() ?? string.Empty;
        }

        // add parameters
        if (sub.Channel.Header?.Any() ?? false)
        {
            foreach (string header in sub.Channel.Header)
            {
                int index = header.IndexOf(':');
                if (index == -1)
                {
                    index = header.IndexOf('=');
                }

                if (index == -1)
                {
                    continue;
                }

                string key = header.Substring(0, index).Trim();
                string value = header.Substring(index + 1).Trim();

                if (!common.Parameters.ContainsKey(key))
                {
                    common.Parameters.Add(key, new());
                }

                common.Parameters[key].Add(value.ToString());
            }

        }

        // add filters
        exts = sub.CriteriaElement?.Extension?.Where(e => e.Url.Equals(_filterCriteriaUrl)) ?? null;
        if (exts?.Any() ?? false)
        {
            foreach (string criteria in exts.Select(e => e.Value.ToString() ?? string.Empty))
            {
                if (string.IsNullOrEmpty(criteria))
                {
                    continue;
                }

                string key;
                string resourceType;
                string value;

                int index = criteria.IndexOf('?');
                if (index == -1)
                {
                    key = "-";
                    resourceType = string.Empty;
                    value = criteria;
                }
                else
                {
                    key = criteria.Substring(0, index);
                    resourceType = key;
                    value = criteria.Substring(index + 1);
                }

                if (!common.Filters.ContainsKey(key))
                {
                    common.Filters.Add(key, new());
                }

                string[] queryParams = value.Split('&');
                
                foreach (string queryParam in queryParams)
                {
                    string[] components = queryParam.Split('=');

                    if (components.Length != 2)
                    {
                        continue;
                    }

                    string[] keyComponents = components[0].Split(":");

                    common.Filters[key].Add(new(
                        resourceType,
                        keyComponents[0],
                        string.Empty,                               // TODO: figure out prefix based on type info we don't have here
                        (keyComponents.Length > 1) ? keyComponents[1] : string.Empty,
                        components[1]));
                }
            }
        }

        return true;
    }
}
