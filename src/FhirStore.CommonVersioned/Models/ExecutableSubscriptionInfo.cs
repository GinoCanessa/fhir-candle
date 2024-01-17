// <copyright file="ExecutableSubscriptionInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using FhirCandle.Models;
using Hl7.FhirPath;

namespace FhirCandle.Models;

/// <summary>An executable subscription tree.</summary>
public class ExecutableSubscriptionInfo
{
    /// <summary>Values that represent interaction types.</summary>
    public enum InteractionTypes
    {
        Create,
        Update,
        Delete
    }

    /// <summary>An interaction only trigger.</summary>
    /// <param name="OnCreate">True to on create.</param>
    /// <param name="OnUpdate">True to on update.</param>
    /// <param name="OnDelete">True to on delete.</param>
    public record class InteractionOnlyTrigger(
        bool OnCreate,
        bool OnUpdate,
        bool OnDelete);

    /// <summary>A compiled FHIR path trigger.</summary>
    /// <param name="OnCreate">       True to on create.</param>
    /// <param name="OnUpdate">       True to on update.</param>
    /// <param name="OnDelete">       True to on delete.</param>
    /// <param name="FhirPathTrigger">The FHIR path trigger.</param>
    public record class CompiledFhirPathTrigger(
        bool OnCreate,
        bool OnUpdate,
        bool OnDelete,
        CompiledExpression FhirPathTrigger);

    /// <summary>A compiled query trigger.</summary>
    /// <param name="OnCreate">        True to on create.</param>
    /// <param name="OnUpdate">        True to on update.</param>
    /// <param name="OnDelete">        True to on delete.</param>
    /// <param name="PreviousTest">    The previous test.</param>
    /// <param name="CreateAutoFails"> True to create automatic fails.</param>
    /// <param name="CreateAutoPasses">True to create automatic passes.</param>
    /// <param name="CurrentTest">     The current test.</param>
    /// <param name="DeleteAutoFails"> True to delete the automatic fails.</param>
    /// <param name="DeleteAutoPasses">True to delete the automatic passes.</param>
    /// <param name="RequireBothTests">True to require both tests.</param>
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

    /// <summary>Gets or sets the interaction triggers.</summary>
    public IEnumerable<InteractionOnlyTrigger> InteractionTriggers { get; set; } = Array.Empty<InteractionOnlyTrigger>();

    /// <summary>Gets or sets the FHIR path triggers.</summary>
    public IEnumerable<CompiledFhirPathTrigger> FhirPathTriggers { get; set; } = Array.Empty<CompiledFhirPathTrigger>();

    /// <summary>Gets or sets the query triggers.</summary>
    public IEnumerable<CompiledQueryTrigger> QueryTriggers { get; set; } = Array.Empty<CompiledQueryTrigger>();

    /// <summary>Gets or sets the subscription filters, by subscription id.</summary>
    public Dictionary<string, List<ParsedSearchParameter>> FiltersBySubscription { get; set; } = new();

    public ParsedResultParameters? AdditionalContext { get; set; } = null;
}
