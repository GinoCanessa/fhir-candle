// <copyright file="ExecutableSubscriptionInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using FhirStore.Models;
using Hl7.FhirPath;

namespace FhirStore.Models;

/// <summary>An executable subscription tree.</summary>
public class ExecutableSubscriptionInfo
{
    /// <summary>Gets or sets URL of the topic.</summary>
    public string TopicUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the compiled topic triggers for this subscription topic.</summary>
    public List<CompiledExpression> CompiledTopicTriggers { get; set; } = new();

    /// <summary>Gets or sets the subscription filters, by subscription id.</summary>
    public Dictionary<string, List<ParsedSearchParameter>> FiltersBySubscription { get; set; } = new();
}
