// <copyright file="SmartClientRegistration.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.Json.Serialization;

namespace FhirCandle.Smart;

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

    /// <summary>Gets or sets the keys.</summary>
    [JsonPropertyName("keys")]
    public IEnumerable<SmartJwksKey> Keys { get; set; } = Array.Empty<SmartJwksKey>();

    /// <summary>A smart jwks key.</summary>
    public class SmartJwksKey
    {
        /// <summary>Gets or sets the family of cryptographic algorithms used with the key.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("kty")]
        public string? KeyType { get; set; } = null;

        /// <summary>Gets or sets how the key was meant to be used.
        /// - "sig" (signature)
        /// - "enc" (encryption)</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("use")]
        public string? Use { get; set; } = null;

        /// <summary>
        /// Gets or sets the key operations.
        /// - "sign" (compute digital signature or MAC)
        /// - "verify" (verify digital signature or MAC)
        /// - "encrypt" (encrypt content)
        /// - "decrypt" (decrypt content and validate decryption, if applicable)
        /// - "wrapKey" (encrypt key)
        /// - "unwrapKey" (decrypt key and validate decryption, if applicable)
        /// - "deriveKey" (derive key)
        /// - "deriveBits" (derive bits not to be used as a key)
        /// </summary>
        [JsonPropertyName("key_ops")]
        public IEnumerable<string>? KeyOperaions { get; set; } = Array.Empty<string>();

        /// <summary>Gets or sets the specific cryptographic algorithm used with the key.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("alg")]
        public string? Algorithm { get; set; } = null;

        /// <summary>Gets or sets the identifier of the key.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("kid")]
        public string? KeyId { get; set; } = null;

        /// <summary>
        /// Gets or sets the URI that refers to a resource for an X.509 public key certificate or
        /// certificate chain.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("x5u")]
        public string? X5U { get; set; } = null;


        /// <summary>Gets or sets a chain of one or more PKIX certificates.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("x5c")]
        public string? X5C { get; set; } = null;

        /// <summary>
        /// Gets or sets the base64url-encoded SHA-1 thumbprint (a.k.a. digest) of the DER encoding of an
        /// X.509 certificate.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("x5t")]
        public string? X5T { get; set; } = null;

        /// <summary>Gets or sets the base64url-encoded SHA-256 thumbprint (a.k.a. digest) of the DER encoding of an X.509 certificate.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("x5t#S256")]
        public string? X5T256 { get; set; } = null;

        /// <summary>Gets or sets the rsa modulus.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("n")]
        public string? RsaModulus { get; set; } = null;

        /// <summary>Gets or sets the rsa exponent.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("e")]
        public string? RsaExponent { get; set; } = null;

        /// <summary>Gets or sets the ecdsa curve.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("crv")]
        public string? EcdsaCurve { get; set; } = null;

        /// <summary>Gets or sets the ecdsa x coordinate.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("x")]
        public string? EcdsaX { get; set; } = null;

        /// <summary>Gets or sets the ecdsa y coordinate.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonPropertyName("y")]
        public string? EcdsaY { get; set; } = null;
    }
}
