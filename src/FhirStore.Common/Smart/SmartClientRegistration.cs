// <copyright file="SmartClientRegistration.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Serialization;

namespace FhirCandle.Smart;

/// <summary>A smart client registration.</summary>
public class SmartClientRegistration
{
    /// <summary>Gets or sets the redirect uris.</summary>
    [JsonPropertyName("redirect_uris")]
    public IEnumerable<string>? RedirectUris { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the name of the client.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; } = null;

    /// <summary>Gets or sets the token endpoint authentication method.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; } = null;

    /// <summary>Gets or sets URI of the logo.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; set; } = null;

    /// <summary>Gets or sets URI of the policy.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("policy_uri")]
    public string? PolicyUri { get; set; } = null;

    /// <summary>Gets or sets URI of the jwks.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; } = null;

    /// <summary>Gets or sets the set the key belongs to.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("keys")]
    public JsonWebKeySet KeySet { get; set; } = new();
}
