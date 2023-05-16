// <copyright file="SubscriptionConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;

namespace FhirStore.Versioned.Shims.Subscriptions;

/// <summary>A subscription format converter.</summary>
public class SubscriptionConverter
{
    private FhirJsonParser _jsonParser = new(new ParserSettings()
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
    });

    private FhirJsonSerializationSettings _jsonSerializerSettings = new()
    {
        AppendNewLine = false,
        Pretty = false,
        IgnoreUnknownElements = true,
    };

    private FhirXmlParser _xmlParser = new(new ParserSettings()
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
    });

    private FhirXmlSerializationSettings _xmlSerializerSettings = new()
    {
        AppendNewLine = false,
        Pretty = false,
        IgnoreUnknownElements = true,
    };

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

    /// <summary>
    /// Serialize one or more subscription events into the desired format and content level.
    /// </summary>
    /// <param name="subscription">    The subscription the events belong to.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentType">     Override for the content type specified in the subscription.</param>
    /// <param name="contentLevel">    Override for the content level specified in the subscription.</param>
    /// <returns></returns>
    public string SerializeSubscriptionEvents(
        ParsedSubscription subscription,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string baseUrl,
        string contentType = "",
        string contentLevel = "")
    {
        if (string.IsNullOrEmpty(contentLevel))
        {
            contentLevel = subscription.ContentLevel;
        }

        // create our notification bundle
        Bundle bundle = new()
        {
            Type = Bundle.BundleType.SubscriptionNotification,
            Timestamp = DateTimeOffset.Now,
            Entry = new(),
        };

        if (!Enum.TryParse(subscription.CurrentStatus, out SubscriptionStatusCodes statusCode))
        {
            statusCode = SubscriptionStatusCodes.Active;
        }

        if (!Enum.TryParse(notificationType, out SubscriptionStatus.SubscriptionNotificationType notificationTypeCode))
        {
            notificationTypeCode = SubscriptionStatus.SubscriptionNotificationType.EventNotification;
        }

        // create our status resource
        SubscriptionStatus status = new()
        {
            Subscription = new ResourceReference("Subscription/" + subscription.Id),
            Topic = subscription.TopicUrl,
            EventsSinceSubscriptionStart = subscription.CurrentEventCount,
            Status = statusCode,
            Type = notificationTypeCode,
            NotificationEvent = new(),
        };

        // add a status placeholder to the bundle
        bundle.Entry.Add(new Bundle.EntryComponent());

        HashSet<string> addedResources = new();

        bool isEmpty = contentLevel.Equals("empty", StringComparison.Ordinal);
        bool isFullResource = contentLevel.Equals("full-resource", StringComparison.Ordinal);

        // determine behavior of no event numbers
        if (!eventNumbers.Any())
        {
            // query-event should send all
            if (notificationType.Equals("query-event"))
            {
                eventNumbers = subscription.GeneratedEvents.Keys;
            }
            // others send the most recent if there is one
            else if (subscription.GeneratedEvents.Any())
            {
                eventNumbers = new long[] { subscription.GeneratedEvents.Keys.Last() };
            }
            else
            {
                eventNumbers = Array.Empty<long>();
            }
        }

        // iterate over the events
        foreach (long eventNumber in eventNumbers)
        {
            if (!subscription.GeneratedEvents.ContainsKey(eventNumber))
            {
                continue;
            }

            SubscriptionEvent se = subscription.GeneratedEvents[eventNumber];

            // check for empty notifications
            if (isEmpty)
            {
                // add just this event number to our status
                status.NotificationEvent.Add(new()
                {
                    EventNumber = eventNumber,
                    Timestamp = se.Timestamp,
                });

                // empty notifications do not contain bundle entries
                continue;
            }

            Resource r = (Resource)se.Focus;
            string relativeUrl = $"{r.TypeName}/{r.Id}";

            // add this event to our status
            status.NotificationEvent.Add(new()
            {
                EventNumber = eventNumber,
                Focus = new ResourceReference(relativeUrl),
                AdditionalContext = (se.AdditionalContext?.Any() ?? false)
                    ? se.AdditionalContext.Select(o => new ResourceReference($"{((Resource)o).TypeName}/{((Resource)o).Id}")).ToList()
                    : new List<ResourceReference>(),
                Timestamp = se.Timestamp,
            });

            // add the focus to our bundle
            if (!addedResources.Contains(relativeUrl))
            {
                bundle.Entry.Add(new Bundle.EntryComponent()
                {
                    FullUrl = baseUrl + "/" + relativeUrl,
                    Resource = isFullResource ? r : null,
                });

                addedResources.Add(relativeUrl);
            }

            // add any additional context
            if (se.AdditionalContext?.Any() ?? false)
            {
                foreach (object ac in se.AdditionalContext)
                {
                    Resource acr = (Resource)ac;
                    string acrRelative = $"{acr.TypeName}/{acr.Id}";

                    if (!addedResources.Contains(acrRelative))
                    {
                        bundle.Entry.Add(new Bundle.EntryComponent()
                        {
                            FullUrl = baseUrl + "/" + acrRelative,
                            Resource = isFullResource ? acr : null,
                        });

                        addedResources.Add(acrRelative);
                    }
                }
            }
        }

        // set our status information in our bundle
        bundle.Entry[0].Resource = status;

        // serialize our bundle
        switch (contentType)
        {
            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                return bundle.ToXml(_xmlSerializerSettings);

            // default to JSON
            case "application/json":
            case "application/fhir+json":
            default:
                return bundle.ToJson(_jsonSerializerSettings);
        }
    }
}
