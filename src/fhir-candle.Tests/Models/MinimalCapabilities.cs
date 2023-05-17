// <copyright file="MinimalCapabilities.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace fhir.candle.Tests.Models;

/// <summary>A minimal CapabilityStatement structure, for fast deserialization.</summary>
public class MinimalCapabilities
{
    /// <summary>A minimal CapabilityStatment.rest.interaction / CapabilityStatment.rest.resource.interaction structure.</summary>
    public class MinimalInteraction
    {
        /// <summary>Gets or sets the code.</summary>
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;
    }

    /// <summary>A minimal search parameter structure.</summary>
    public class MinimalSearchParam
    {
        /// <summary>Gets or sets the name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets URL of the document.</summary>
        [JsonPropertyName("definition")]
        public string Url { get; set; } = string.Empty;

        /// <summary>Gets or sets the type of the search.</summary>
        [JsonPropertyName("type")]
        public string SearchType { get; set; } = string.Empty;
    }

    /// <summary>A minimal operation structure.</summary>
    public class MinimalOperation
    {
        /// <summary>Gets or sets the name.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Gets or sets URL of the document.</summary>
        [JsonPropertyName("definition")]
        public string Url { get; set; } = string.Empty;
    }


    /// <summary>A minimal resource CapabilityStatement.Rest.Resource strucutre.</summary>
    public class MinimalResource
    {
        /// <summary>Gets or sets the type of the resource.</summary>
        [JsonPropertyName("type")]
        public string ResourceType { get; set; } = string.Empty;

        /// <summary>Gets or sets the interactions.</summary>
        [JsonPropertyName("interaction")]
        public IEnumerable<MinimalInteraction>? Interactions { get; set; } = Array.Empty<MinimalInteraction>();

        /// <summary>Gets or sets the supported includes.</summary>
        [JsonPropertyName("searchInclude")]
        public IEnumerable<string>? SupportedIncludes { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets the supported reverse includes.</summary>
        [JsonPropertyName("searchRevInclude")]
        public IEnumerable<string>? SupportedRevIncludes { get; set; } = Array.Empty<string>();

        /// <summary>Gets options for controlling the search.</summary>
        [JsonPropertyName("searchParam")]
        public IEnumerable<MinimalSearchParam>? SearchParams { get; set; } = Array.Empty<MinimalSearchParam>();

        /// <summary>Gets or sets the operations.</summary>
        [JsonPropertyName("operation")]
        public IEnumerable<MinimalOperation>? Operations { get; set; } = Array.Empty<MinimalOperation>();
    }

    /// <summary>A minimal CapabilityStatement.Rest structure</summary>
    public class MinimalRest
    {
        /// <summary>Gets or sets the mode.</summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; } = string.Empty;

        /// <summary>Gets or sets the resources.</summary>
        [JsonPropertyName("resource")]
        public IEnumerable<MinimalResource>? Resources { get; set; } = null;

        /// <summary>Gets or sets the interactions.</summary>
        [JsonPropertyName("interaction")]
        public IEnumerable<MinimalInteraction> Interactions { get; set; } = Array.Empty<MinimalInteraction>();

        /// <summary>Gets options for controlling the search.</summary>
        [JsonPropertyName("searchParam")]
        public IEnumerable<MinimalSearchParam>? SearchParams { get; set; } = Array.Empty<MinimalSearchParam>();

        /// <summary>Gets or sets the operations.</summary>
        [JsonPropertyName("operation")]
        public IEnumerable<MinimalOperation>? Operations { get; set; } = Array.Empty<MinimalOperation>();

        /// <summary>Gets or sets the compartments.</summary>
        [JsonPropertyName("compartment")]
        public IEnumerable<string>? Compartments { get; set; } = Array.Empty<string>();
    }

    /// <summary>Gets or sets the identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Gets or sets URI of the document.</summary>
    [JsonPropertyName("url")]
    public string Uri { get; set; } = string.Empty;

    /// <summary>Gets or sets the version.</summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>Gets or sets the name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the kind.</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets the FHIR version.</summary>
    [JsonPropertyName("fhirVersion")]
    public string FhirVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the REST.</summary>
    [JsonPropertyName("rest")]
    public IEnumerable<MinimalRest>? Rest { get; set; } = null;

}
