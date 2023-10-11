// <copyright file="SmartAuthorization.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace fhir.candle.Models;

/// <summary>SMART authorization parameters.</summary>
public class SmartAuthorization
{
    /// <summary>Authorization request parameters.</summary>
    public class RequestParams
    {
        [JsonPropertyName("response_type")]
        public string ResponseType { get; set; } = string.Empty;

        [JsonPropertyName("client_id")]
        public string ClientId { get; set; } = string.Empty;

        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; set; } = string.Empty;

        [JsonPropertyName("launch")]
        public string? Launch { get; set; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("aud")]
        public string Audience { get; set; } = string.Empty;

        [JsonPropertyName("code_challenge")]
        public string? PkceChallenge { get; set; } = string.Empty;

        [JsonPropertyName("code_challenge_method")]
        public string? PkceMethod { get; set; } = string.Empty;
    }

    /// <summary>Gets or initializes the key.</summary>
    public string Key { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Gets or initializes the tenant name.</summary>
    public required string Tenant { get; init; }

    /// <summary>Gets or initializes options for controlling the request.</summary>
    public required RequestParams RequestParameters { get; init; }

    /// <summary>Gets or initializes the remote IP address.</summary>
    public required string RemoteIpAddress { get; init; }

    /// <summary>Gets or initializes the created.</summary>
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or initializes the last accessed.</summary>
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the expiration time.</summary>
    public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddMinutes(5);
}
