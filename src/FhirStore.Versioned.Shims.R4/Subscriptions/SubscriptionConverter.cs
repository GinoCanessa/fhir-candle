﻿// <copyright file="SubscriptionConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Globalization;
using FhirStore.Extensions;
using FhirStore.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using static Hl7.Fhir.Model.Subscription;

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
            ChannelCode = sub.Channel.Type == null
                ? string.Empty
                : Hl7.Fhir.Utility.EnumUtility.GetLiteral(sub.Channel.Type),
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

    private string GetPVal(Hl7.Fhir.Model.Parameters param, string name)
    {
        Parameters.ParameterComponent? pc = param.Parameter.First(p => p.Name.Equals(name, StringComparison.Ordinal));

        if (pc == null)
        {
            return string.Empty;
        }

        switch (pc.Value)
        {
            case ResourceReference valRef:
                return valRef.Reference?.ToString() ?? string.Empty;
        }

        return pc.Value.ToString() ?? string.Empty;
    }

    private string GetPVal(Hl7.Fhir.Model.Parameters.ParameterComponent pc, string name)
    {
        if (pc == null)
        {
            return string.Empty;
        }

        switch (pc.Value)
        {
            case ResourceReference valRef:
                return valRef.Reference?.ToString() ?? string.Empty;
        }

        return pc.Value.ToString() ?? string.Empty;
    }

    private IEnumerable<ParsedSubscriptionStatus.ParsedNotificationEvent> GetNotEvents(Parameters status)
    {
        if (!(status.Parameter?.Any(p => p.Name.Equals("notification-event", StringComparison.Ordinal)) ?? false))
        {
            return Array.Empty<ParsedSubscriptionStatus.ParsedNotificationEvent>();
        }

        List<ParsedSubscriptionStatus.ParsedNotificationEvent> eventList = new();

        foreach (Parameters.ParameterComponent pc in status.Parameter.Where(p => p.Name.Equals("notification-event", StringComparison.Ordinal)))
        {
            eventList.Add(new ParsedSubscriptionStatus.ParsedNotificationEvent()
            {
                Id = string.Empty,
                EventNumber = long.TryParse(GetPVal(pc, "event-number"), out long en)
                            ? en
                            : null,
                Timestamp = DateTimeOffset.TryParse(GetPVal(pc, "timestamp"), null, DateTimeStyles.RoundtripKind, out DateTimeOffset dt)
                            ? dt
                            : null,
                FocusReference = GetPVal(pc, "focus"),
                AdditionalContextReferences =
                            pc.Part.Where(np => np.Name.Equals("additional-context"))
                            .Select(ac => (ac.Value as ResourceReference)?.Reference ?? string.Empty)
                            ?? Array.Empty<string>(),
            });
        }

        return eventList.ToArray();
    }

    /// <summary>Attempts to parse a ParsedSubscriptionStatus from the given object.</summary>
    /// <param name="subscriptionStatus">The subscription.</param>
    /// <param name="common">      [out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object subscriptionStatus, string bundleId, out ParsedSubscriptionStatus common)
    {
        if ((subscriptionStatus == null) ||
            (subscriptionStatus is not Hl7.Fhir.Model.Parameters status))
        {
            common = null!;
            return false;
        }

        common = new()
        {
            LocalBundleId = status.Id,
            SubscriptionReference = GetPVal(status, "subscription"),
            SubscriptionTopicCanonical = GetPVal(status, "topic"),
            Status = GetPVal(status, "status"),
            NotificationType =
                GetPVal(status, "type").TryFhirEnum(out ParsedSubscription.NotificationTypeCodes nt)
                ? nt
                : null,
            EventsSinceSubscriptionStart = long.TryParse(GetPVal(status, "events-since-subscription-start"), out long count)
                ? count
                : null,
            NotificationEvents = GetNotEvents(status),
        };

        return true;
    }

    /// <summary>Status for subscription.</summary>
    /// <param name="subscription">    The subscription.</param>
    /// <param name="notificationType">Type of the notification.</param>
    /// <param name="baseUrl">         URL of the base.</param>
    /// <returns>A Hl7.Fhir.Model.Resource.</returns>
    public Hl7.Fhir.Model.Resource StatusForSubscription(
        ParsedSubscription subscription,
        string notificationType,
        string baseUrl)
    {
        return new Hl7.Fhir.Model.Parameters()
        {
            Parameter = new()
            {
                new()
                {
                    Name = "subscription",
                    Value = new FhirString(baseUrl + "/Subscription/" + subscription.Id)
                },
                new()
                {
                    Name = "topic",
                    Value = new Canonical(subscription.TopicUrl),
                },
                new()
                {
                    Name = "status",
                    Value = new Code(subscription.CurrentStatus)
                },
                new()
                {
                    Name = "type",
                    Value = new Code(notificationType),
                },
                new()
                {
                    Name = "events-since-subscription-start",
                    Value = new FhirString(subscription.CurrentEventCount.ToString())
                },
            },
        };
    }

    /// <summary>
    /// Build a bundle of subscription events into the desired format and content level.
    /// </summary>
    /// <param name="subscription">    The subscription the events belong to.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentLevel">    Override for the content level specified in the subscription.</param>
    /// <returns></returns>
    public Bundle? BundleForSubscriptionEvents(
        ParsedSubscription subscription,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string baseUrl,
        string contentLevel = "")
    {
        if (string.IsNullOrEmpty(contentLevel))
        {
            contentLevel = subscription.ContentLevel;
        }

        // create our notification bundle
        Bundle bundle = new()
        {
            Type = Bundle.BundleType.History,
            Timestamp = DateTimeOffset.Now,
            Entry = new(),
        };

        // create our status parameters
        Parameters status = new()
        {
            Parameter = new()
            {
                new()
                {
                    Name = "subscription",
                    Value = new FhirString(baseUrl + "/Subscription/" + subscription.Id)
                },
                new()
                {
                    Name = "topic",
                    Value = new Canonical(subscription.TopicUrl),
                },
                new()
                {
                    Name = "status",
                    Value = new Code(subscription.CurrentStatus)
                },
                new()
                {
                    Name = "type",
                    Value = new Code(notificationType),
                },
                new()
                {
                    Name = "events-since-subscription-start",
                    Value = new FhirString(subscription.CurrentEventCount.ToString())
                },
            },
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
                status.Parameter.Add(new()
                {
                    Name = "notification-event",
                    Part = new()
                    {
                        new()
                        {
                            Name = "event-number",
                            Value = new FhirString(eventNumber.ToString()),
                        },
                        new()
                        {
                            Name = "timestamp",
                            Value = new Instant(se.Timestamp),
                        }
                    },
                });

                continue;
            }

            Resource r = (Resource)se.Focus;
            string relativeUrl = $"{r.TypeName}/{r.Id}";

            // add this event to our status
            Parameters.ParameterComponent ne = new()
            {
                Name = "notification-event",
                Part = new()
                {
                    new()
                    {
                        Name = "event-number",
                        Value = new FhirString(eventNumber.ToString()),
                    },
                    new()
                    {
                        Name = "timestamp",
                        Value = new Instant(se.Timestamp),
                    },
                    new()
                    {
                        Name = "focus",
                        Value = new ResourceReference(baseUrl + "/" + relativeUrl)
                    }
                },
            };

            if (se.AdditionalContext?.Any() ?? false)
            {
                ne.Part.AddRange(
                    se.AdditionalContext.Select(o => new Parameters.ParameterComponent()
                    {
                        Name = "additional-context",
                        Value = new ResourceReference($"{((Resource)o).TypeName}/{((Resource)o).Id}"),
                    }));
            }

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
        bundle.Entry[0].FullUrl = $"urn:uuid:{Guid.NewGuid().ToString()}";

        return bundle;
    }
}
