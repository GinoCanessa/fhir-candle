//// <copyright file="SmartClientRegistration.cs" company="Microsoft Corporation">
////     Copyright (c) Microsoft Corporation. All rights reserved.
////     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
//// </copyright>

//using System.Text.Json.Serialization;

//namespace FhirCandle.Smart;

///// <summary>A JSON web key.</summary>
//public class JsonWebKey
//{
//    /// <summary>Gets or sets the family of cryptographic algorithms used with the key.</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("kty")]
//    public string? KeyType { get; set; } = null;

//    /// <summary>
//    /// Gets or sets how the key was meant to be used.
//    /// - "sig" (signature)
//    /// - "enc" (encryption)
//    /// </summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("use")]
//    public string? Use { get; set; } = null;

//    /// <summary>
//    /// Gets or sets the key operations.
//    /// - "sign" (compute digital signature or MAC)
//    /// - "verify" (verify digital signature or MAC)
//    /// - "encrypt" (encrypt content)
//    /// - "decrypt" (decrypt content and validate decryption, if applicable)
//    /// - "wrapKey" (encrypt key)
//    /// - "unwrapKey" (decrypt key and validate decryption, if applicable)
//    /// - "deriveKey" (derive key)
//    /// - "deriveBits" (derive bits not to be used as a key)
//    /// </summary>
//    [JsonPropertyName("key_ops")]
//    public IEnumerable<string>? KeyOperations { get; set; } = Enumerable.Empty<string>();

//    /// <summary>Gets or sets the specific cryptographic algorithm used with the key.</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("alg")]
//    public string? Algorithm { get; set; } = null;

//    /// <summary>Gets or sets the identifier of the key.</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("kid")]
//    public string? KeyId { get; set; } = null;

//    /// <summary>
//    /// Gets or sets the URI that refers to a resource for an X.509 public key certificate or
//    /// certificate chain.
//    /// </summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("x5u")]
//    public string? X5U { get; set; } = null;

//    /// <summary>Gets or sets a chain of one or more PKIX certificates.</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("x5c")]
//    public string? X5C { get; set; } = null;

//    /// <summary>
//    /// Gets or sets the base64url-encoded SHA-1 thumbprint (a.k.a. digest) of the DER encoding of an
//    /// X.509 certificate.
//    /// </summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("x5t")]
//    public string? X5T { get; set; } = null;

//    /// <summary>Gets or sets the base64url-encoded SHA-256 thumbprint (a.k.a. digest) of the DER encoding of an X.509 certificate.</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("x5t#S256")]
//    public string? X5T256 { get; set; } = null;

//    /// <summary>Gets or sets the RSA modulus (n).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("n")]
//    public string? RsaModulus { get; set; } = null;

//    /// <summary>Gets or sets the RSA exponent (e).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("e")]
//    public string? RsaExponent { get; set; } = null;

//    /// <summary>Gets or sets the rsa private exponent (d).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("d")]
//    public string? RsaPrivateExponent { get; set; } = null;

//    /// <summary>Gets or sets the rsa first prime factor (p).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("p")]
//    public string? RsaFirstPrimeFactor { get; set; } = null;

//    /// <summary>Gets or sets the rsa second prime factor (q).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("q")]
//    public string? RsaSecondPrimeFactor { get; set; } = null;

//    /// <summary>Gets or sets the rsa first factor CRT exponent (dp).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("dp")]
//    public string? RsaFirstFactorCrtExponent { get; set; } = null;

//    /// <summary>Gets or sets the rsa second factor CRT exponent (dq).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("dq")]
//    public string? RsaSecondFactorCrtExponent { get; set; } = null;

//    /// <summary>Gets or sets the rsa first CRT coefficient (qi).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("qi")]
//    public string? RsaFirstCrtCoefficient { get; set; } = null;

//    /// <summary>Gets or sets information describing the rsa other primes (oth).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("oth")]
//    public string? RsaOtherPrimesInfo { get; set; } = null;

//    /// <summary>Gets or sets the EC curve (crv), using the friendly name ("P-384").</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("crv")]
//    public string? EcCurve { get; set; } = null;

//    /// <summary>Gets or sets the EC x coordinate (x).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("x")]
//    public string? EcX { get; set; } = null;

//    /// <summary>Gets or sets the EC y coordinate (y).</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("y")]
//    public string? EcY { get; set; } = null;

//    /// <summary>Gets or sets the EC private key.</summary>
//    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
//    [JsonPropertyName("d")]
//    public string? EcPrivateKey { get; set; } = null;
//}
