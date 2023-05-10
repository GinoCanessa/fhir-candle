// <copyright file="ParsedSubscriptionTopic.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirStore.Models;

/// <summary>A parsed subscription topic.</summary>
public class ParsedSubscriptionTopic
{
    /// <summary>A resource trigger.</summary>
    /// <param name="ResourceType">      Type of the resource.</param>
    /// <param name="OnCreate">          True to on create.</param>
    /// <param name="OnUpdate">          True to on update.</param>
    /// <param name="OnDelete">          True to on delete.</param>
    /// <param name="QueryPrevious">     The query previous.</param>
    /// <param name="CreateAutoPass">    True to create automatic pass.</param>
    /// <param name="CreateAutoFail">    True to create automatic fail.</param>
    /// <param name="QueryCurrent">      The query current.</param>
    /// <param name="RequireBothQueries">True to require both queries.</param>
    /// <param name="FhirPathCritiera">  The FHIR path critiera.</param>
    public readonly record struct ResourceTrigger(
        string ResourceType,
        bool OnCreate,
        bool OnUpdate,
        bool OnDelete,
        string QueryPrevious,
        bool CreateAutoPass,
        bool CreateAutoFail,
        string QueryCurrent,
        bool RequireBothQueries,
        string FhirPathCritiera);

    /// <summary>An event trigger.</summary>
    /// <param name="ResourceType">Type of the resource.</param>
    /// <param name="EventSystem"> The event system.</param>
    /// <param name="EventCode">   The event code.</param>
    /// <param name="EventText">   The event text.</param>
    public readonly record struct EventTrigger(
        string ResourceType,
        string EventSystem,
        string EventCode,
        string EventText);

    /// <summary>An allowed filter.</summary>
    /// <param name="ResourceType">      Type of the resource.</param>
    /// <param name="Name">              The name of the allowed filter parameter.</param>
    /// <param name="Url">               URL of the resource.</param>
    /// <param name="FhirPathExpression">The FHIR path expression.</param>
    public readonly record struct AllowedFilter(
        string ResourceType,
        string Name,
        string Url);

    /// <summary>A notification shape.</summary>
    /// <param name="ResourceType">   Type of the resource.</param>
    /// <param name="Includes">       The includes.</param>
    /// <param name="ReverseIncludes">The reverse includes.</param>
    public readonly record struct NotificationShape(
        string ResourceType,
        List<string> Includes,
        List<string> ReverseIncludes);

    /// <summary>Gets or initializes the identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets or initializes URL of this subscription topic.</summary>
    public required string Url { get; init; }

    /// <summary>Gets or sets the resource triggers.</summary>
    public Dictionary<string, List<ResourceTrigger>> ResourceTriggers { get; set; } = new();

    /// <summary>Gets or sets the event triggers.</summary>
    public Dictionary<string, List<EventTrigger>> EventTriggers { get; set; } = new();

    /// <summary>Gets or sets the allowed filters.</summary>
    public Dictionary<string, List<AllowedFilter>> AllowedFilters { get; set; } = new();

    /// <summary>Gets or initializes the notification shapes.</summary>
    public Dictionary<string, List<NotificationShape>> NotificationShapes { get; set; } = new();
}
