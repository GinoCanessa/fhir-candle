// <copyright file="AuthorizationInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;
using static System.Formats.Asn1.AsnWriter;

namespace fhir.candle.Models;

/// <summary>Authorization information for internal tracking.</summary>
public class AuthorizationInfo
{
    private SmartRequest _requestParameters = null!;

    /// <summary>Authorization request parameters.</summary>
    public class SmartRequest
    {
        [JsonPropertyName("response_type")]
        public string ResponseType { get; init; } = string.Empty;

        [JsonPropertyName("client_id")]
        public string ClientId { get; init; } = string.Empty;

        [JsonPropertyName("redirect_uri")]
        public string RedirectUri { get; init; } = string.Empty;

        [JsonPropertyName("launch")]
        public string? Launch { get; init; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scope { get; init; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; init; } = string.Empty;

        [JsonPropertyName("aud")]
        public string Audience { get; init; } = string.Empty;

        [JsonPropertyName("code_challenge")]
        public string? PkceChallenge { get; init; } = string.Empty;

        [JsonPropertyName("code_challenge_method")]
        public string? PkceMethod { get; init; } = string.Empty;
    }

    /// <summary>A smart response.</summary>
    public record class SmartResponse
    {
        [JsonPropertyName("need_patient_banner")]
        public bool NeedPatientBanner { get; init; } = false;

        [JsonPropertyName("smart_style_url")]
        public string SmartStyleUrl { get; init; } = string.Empty;

        [JsonPropertyName("patient")]
        public string PatientId { get; init; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; init; } = string.Empty;

        [JsonPropertyName("scope")]
        public string Scopes { get; init; } = string.Empty;

        [JsonPropertyName("client_id")]
        public string ClientId { get; init; } = string.Empty;

        [JsonPropertyName("expires_in")]
        public int ExpiresInSeconds { get; init; } = 60 * 10;

        [JsonPropertyName("id_token")]
        public string IdToken { get; init; } = string.Empty;

        [JsonPropertyName("access_token")]
        public string AccessToken { get; init; } = string.Empty;

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; init; } = string.Empty;
    }

    /// <summary>Gets or initializes the key.</summary>
    public string Key { get; init; } = Guid.NewGuid().ToString();

    /// <summary>Gets or initializes the tenant name.</summary>
    public required string Tenant { get; init; }

    /// <summary>Gets or initializes options for controlling the request.</summary>
    public required SmartRequest RequestParameters 
    { 
        get => _requestParameters;
        init
        {
            _requestParameters = value;

            Scopes = System.Web.HttpUtility.UrlDecode(value.Scope)
                .Split(' ')
                .ToDictionary(s => s, s => true);
        }
    }

    /// <summary>Gets or initializes the remote IP address.</summary>
    public required string RemoteIpAddress { get; init; }

    /// <summary>Gets or initializes the created.</summary>
    public DateTimeOffset Created { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or initializes the last accessed.</summary>
    public DateTimeOffset LastAccessed { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the expiration time.</summary>
    public DateTimeOffset Expires { get; set; } = DateTimeOffset.UtcNow.AddMinutes(10);

    /// <summary>Gets or sets the identifier of the user.</summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or initializes the launch patient.</summary>
    public string LaunchPatient { get; set; } = string.Empty;

    /// <summary>Gets or initializes the launch practitioner.</summary>
    public string LaunchPractitioner { get; set; } = string.Empty;

    /// <summary>Gets or initializes the scopes.</summary>
    public Dictionary<string, bool> Scopes { get; init; } = new();

    /// <summary>Gets or sets the authentication code.</summary>
    public string AuthCode { get; set; } = string.Empty;

    /// <summary>The latest response.</summary>
    public SmartResponse? Response = null;
}
