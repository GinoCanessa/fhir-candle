﻿// <copyright file="CommonSubscription.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.ComponentModel;

namespace FhirStore.Models;

/// <summary>A common subscription.</summary>
public class ParsedSubscription
{
    private long _currentEventCount = 0;
    private Dictionary<long, SubscriptionEvent> _generatedEvents = new();
    private List<string> _notificationErrors = new();

    public enum NotificationTypeCodes
    {
        /// <summary>
        /// The status was generated as part of the setup or verification of a communications channel.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        //[EnumLiteral("handshake", "http://hl7.org/fhir/subscription-notification-type"), Description("Handshake")]
        Handshake,
        /// <summary>
        /// The status was generated to perform a heartbeat notification to the subscriber.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        //[EnumLiteral("heartbeat", "http://hl7.org/fhir/subscription-notification-type"), Description("Heartbeat")]
        Heartbeat,
        /// <summary>
        /// The status was generated for an event to the subscriber.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        //[EnumLiteral("event-notification", "http://hl7.org/fhir/subscription-notification-type"), Description("Event Notification")]
        EventNotification,
        /// <summary>
        /// The status was generated in response to a status query/request.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        //[EnumLiteral("query-status", "http://hl7.org/fhir/subscription-notification-type"), Description("Query Status")]
        QueryStatus,
        /// <summary>
        /// The status was generated in response to an event query/request.
        /// (system: http://hl7.org/fhir/subscription-notification-type)
        /// </summary>
        //[EnumLiteral("query-event", "http://hl7.org/fhir/subscription-notification-type"), Description("Query Event")]
        QueryEvent,
    }

    /// <summary>An allowed filter.</summary>
    /// <param name="ResourceType">Type of the resource.</param>
    /// <param name="Name">        The name of the filter parameter.</param>
    /// <param name="Comparator">  The comparator.</param>
    /// <param name="Modifier">    The modifier.</param>
    /// <param name="Value">       The value.</param>
    public readonly record struct SubscriptionFilter(
        string ResourceType,
        string Name,
        string Comparator,
        string Modifier,
        string Value);

    /// <summary>Gets or initializes the identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets or initializes URL of the topic.</summary>
    public required string TopicUrl { get; init; }

    /// <summary>Gets or initializes the expressed filters by resource type.</summary>
    public Dictionary<string, List<SubscriptionFilter>> Filters { get; set; } = new();

    /// <summary>Gets or initializes the channel system.</summary>
    public string ChannelSystem { get; set; } = string.Empty;

    /// <summary>Gets or initializes the channel code.</summary>
    public string ChannelCode { get; set; } = string.Empty;

    /// <summary>Gets or sets the endpoint.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets options for controlling the subscription.</summary>
    public Dictionary<string, List<string>> Parameters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gets or sets the heartbeat seconds.</summary>
    public int HeartbeatSeconds { get; set; } = 0;

    /// <summary>Gets or sets the timeout seconds.</summary>
    public int TimeoutSeconds { get; set; } = 0;

    /// <summary>Gets or sets the type of the content.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the content level.</summary>
    public string ContentLevel { get; set; } = string.Empty;

    /// <summary>Gets or sets the maximum events per notification.</summary>
    public int MaxEventsPerNotification { get; set; } = 0;

    /// <summary>Gets or sets the current status of the subscription.</summary>
    public string CurrentStatus { get; set; } = "active";

    /// <summary>Gets or sets the system tick (time) when the last communication was sent.</summary>
    public long LastCommunicationTicks { get; set; } = 0;

    /// <summary>Gets or sets the number of current events.</summary>
    public long CurrentEventCount { get => _currentEventCount; }

    /// <summary>Increment event count.</summary>
    /// <returns>A long.</returns>
    public long IncrementEventCount()
    {
        return Interlocked.Increment(ref _currentEventCount);
    }

    /// <summary>Gets or sets the generated events.</summary>
    public Dictionary<long, SubscriptionEvent> GeneratedEvents { get => _generatedEvents; }

    public void RegisterEvent(SubscriptionEvent subscriptionEvent)
    {
        if (_generatedEvents.ContainsKey(subscriptionEvent.EventNumber))
        {
            // TODO: for now just overwrite, figure out what we want to do later
            _generatedEvents[subscriptionEvent.EventNumber] = subscriptionEvent;
            return;
        }

        _generatedEvents.Add(subscriptionEvent.EventNumber, subscriptionEvent);
    }

    /// <summary>Gets or sets the notification errors.</summary>
    public List<string> NotificationErrors { get => _notificationErrors; }

    /// <summary>Registers the error described by error.</summary>
    /// <param name="error">The error.</param>
    public void RegisterError(string error)
    {
        _notificationErrors.Add(error);
        Console.WriteLine($" <<< Subscription/{Id}: {error}");
    }

    /// <summary>Clears the errors.</summary>
    public void ClearErrors()
    {
        _notificationErrors.Clear();
    }
}