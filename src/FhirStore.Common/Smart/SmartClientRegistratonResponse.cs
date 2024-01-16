// <copyright file="SmartClientRegistratonResponse.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace FhirCandle.Smart;

/// <summary>A smart client registraton response.</summary>
public class SmartClientRegistratonResponse
{
    /// <summary>Gets or sets the identifier of the client.</summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;


    /// <summary>Gets or sets the issued at (seconds UNIX time).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("client_id_issued_at")]
    public long? IssuedAt { get; set; } = null;

    /// <summary>Gets or sets the client secret.</summary>
    [JsonPropertyName("client_secret")]
    public string ClientSecret { get; set;} = string.Empty;

    /// <summary>Gets or sets the secret expires at.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("client_secret_expires_at")]
    public long? SecretExpiresAt { get; set; } = null;
}
