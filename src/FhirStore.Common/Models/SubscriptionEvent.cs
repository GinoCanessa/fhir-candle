// <copyright file="SubscriptionEvent.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>A subscription event.</summary>
public record class SubscriptionEvent
{
    /// <summary>Gets or initializes the subscription id.</summary>
    public required string SubscriptionId { get; init; }

    /// <summary>Gets or initializes subscription topic url.</summary>
    public required string TopicUrl { get; init; }

    /// <summary>Gets or initializes the event number.</summary>
    public required long EventNumber { get; init; }

    /// <summary>Gets or initializes date/time when this event was generated.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;

    /// <summary>Gets or initializes the subscription status at generation (defaults to active).</summary>
    public string StatusAtGeneration { get; init; } = "active";

    /// <summary>Gets or initializes the focus resource (versioned).</summary>
    public required object Focus { get; init; }

    /// <summary>Gets or initializes additional context (enumerable of versioned resources).</summary>
    public IEnumerable<object>? AdditionalContext { get; init; }
}
