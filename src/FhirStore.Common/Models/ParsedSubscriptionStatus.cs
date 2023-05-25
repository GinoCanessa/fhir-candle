// <copyright file="ParsedSubscriptionStatus.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.Text.Json.Serialization;

namespace FhirStore.Models;

public class ParsedSubscriptionStatus
{
    /*
      - id[0..1]: id *simple*
      - meta[0..1]: Meta
      - implicitRules[0..1]: uri
      - language[0..1]: code
      - text[0..1]: Narrative conditions: dom-6
      - contained[0..*]: Resource conditions: dom-2, dom-3, dom-4, dom-5
      - extension[0..*]: Extension
      - modifierExtension[0..*]: Extension
      - status[0..1]: code conditions: sst-2 (W5: FiveWs.status)
        {requested|active|error|off|entered-in-error}
      - type[1..1]: code conditions: sst-1, sst-2 (W5: FiveWs.what[x])
        {handshake|heartbeat|event-notification|query-status|query-event}
      - eventsSinceSubscriptionStart[0..1]: integer64
      - notificationEvent[0..*]: BackboneElement conditions: sst-1
        - id[0..1]: string *simple* conditions: ele-1
        - extension[0..*]: Extension
        - modifierExtension[0..*]: Extension
        - eventNumber[1..1]: integer64
        - timestamp[0..1]: instant
        - focus[0..1]: Reference(Resource)
        - additionalContext[0..*]: Reference(Resource)
      - subscription[1..1]: Reference(Subscription) (W5: FiveWs.why[x])
      - topic[0..1]: canonical(SubscriptionTopic)
      - error[0..*]: CodeableConcept
     */

    public class ParsedNotificationEvent
    {
        public string Id { get; init; } = string.Empty;

        public required long? EventNumber { get; init; }

        public DateTimeOffset? Timestamp { get; init; } = null;

        public string FocusReference { get; init; } = string.Empty;

        public IEnumerable<string>? AdditionalContextReferences { get; init; } = Array.Empty<string>();
    }

    public required string LocalBundleId { get; init; }

    public DateTimeOffset ProcessedDateTime { get; init; } = DateTimeOffset.Now;

    public required string SubscriptionReference { get; init; }

    public required string SubscriptionTopicCanonical { get; init; }

    public string Status { get; init; } = string.Empty;

    /// <summary>Gets or initializes the type of the notification.</summary>
    public required ParsedSubscription.NotificationTypeCodes? NotificationType { get; init; }

    public long? EventsSinceSubscriptionStart { get; init; } = null;

    public IEnumerable<ParsedNotificationEvent> NotificationEvents { get; init; } = Array.Empty<ParsedNotificationEvent>();
}

