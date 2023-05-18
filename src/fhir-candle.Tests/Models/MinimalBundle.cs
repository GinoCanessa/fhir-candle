// <copyright file="MinimalBundle.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace fhir.candle.Tests.Models;

/// <summary>A minimal Bundle structure, for fast deserialization.</summary>
public class MinimalBundle
{
    /// <summary>A minimal Bundle.entry structure.</summary>
    public class MinimalEntry
    {
        /// <summary>A minimal Bundle.entry.search structure.</summary>
        public class MinimalSearch
        {
            /// <summary>Gets or sets the mode.</summary>
            [JsonPropertyName("mode")]
            public string Mode { get; set; } = string.Empty;
        }

        /// <summary>Gets or sets URL of the full.</summary>
        [JsonPropertyName("fullUrl")]
        public string FullUrl { get; set; } = string.Empty;

        /// <summary>Gets or sets the search.</summary>
        [JsonPropertyName("search")]
        public MinimalSearch? Search { get; set; } = null;
    }

    /// <summary>A minimal link.</summary>
    public class MinimalLink
    {
        /// <summary>Gets or sets the relation.</summary>
        [JsonPropertyName("relation")]
        public string Relation { get; set; } = string.Empty;

        /// <summary>Gets or sets URL of the document.</summary>
        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;
    }

    /// <summary>Gets or sets the type of the bundle.</summary>
    [JsonPropertyName("type")]
    public string BundleType { get; set; } = string.Empty;

    /// <summary>Gets or sets the total number of matches, if this is a search bundle. </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; } = -1;

    /// <summary>Gets or sets the links.</summary>
    [JsonPropertyName("link")]
    public IEnumerable<MinimalLink>? Links { get; set; } = null;

    /// <summary>Gets or sets the entries.</summary>
    [JsonPropertyName("entry")]
    public IEnumerable<MinimalEntry>? Entries { get; set; } = null;
}
