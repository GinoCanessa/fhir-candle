// <copyright file="CommonSubscription.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirStore.Models;

/// <summary>A common subscription.</summary>
public class ParsedSubscription
{
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
    public Dictionary<string, List<string>> Parameters { get; set; } = new();

    /// <summary>Gets or sets the heartbeat seconds.</summary>
    public int HeartbeatSeconds { get; set; } = 0;

    /// <summary>Gets or sets the timeout seconds.</summary>
    public int TimeoutSeconds { get; set; } = 0;

    /// <summary>Gets or sets the type of the content.</summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>Gets or sets the content level.</summary>
    public string ContentLevel { get; set;} = string.Empty;

    /// <summary>Gets or sets the maximum events per notification.</summary>
    public int MaxEventsPerNotification { get; set; } = 0;
}
