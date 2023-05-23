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
    public enum InteractionTypes
    {
        Create,
        Update,
        Delete
    }

    public record class InteractionOnlyTrigger(
        bool OnCreate,
        bool OnUpdate,
        bool OnDelete);

    public record class CompiledFhirPathTrigger(
        bool OnCreate,
        bool OnUpdate,
        bool OnDelete,
        CompiledExpression FhirPathTrigger);

    public record class CompiledQueryTrigger(
        bool OnCreate,
        bool OnUpdate,
        bool OnDelete,
        IEnumerable<ParsedSearchParameter> PreviousTest,
        bool CreateAutoFails,
        bool CreateAutoPasses,
        IEnumerable<ParsedSearchParameter> CurrentTest,
        bool DeleteAutoFails,
        bool DeleteAutoPasses,
        bool RequireBothTests);

    /// <summary>Gets or sets URL of the topic.</summary>
    public string TopicUrl { get; set; } = string.Empty;

    public IEnumerable<InteractionOnlyTrigger> InteractionTriggers { get; set; } = Array.Empty<InteractionOnlyTrigger>();

    public IEnumerable<CompiledFhirPathTrigger> FhirPathTriggers { get; set; } = Array.Empty<CompiledFhirPathTrigger>();

    public IEnumerable<CompiledQueryTrigger> QueryTriggers { get; set; } = Array.Empty<CompiledQueryTrigger>();

    /// <summary>Gets or sets the subscription filters, by subscription id.</summary>
    public Dictionary<string, List<ParsedSearchParameter>> FiltersBySubscription { get; set; } = new();
}
