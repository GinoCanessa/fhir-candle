// <copyright file="ClientInfo.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Smart;

namespace FhirCandle.Models;

/// <summary>Information about the client.</summary>
public class ClientInfo
{
    /// <summary>Information about the authentication activity.</summary>
    public readonly record struct ClientActivityRecord
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClientActivityRecord"/> class.
        /// </summary>
        public ClientActivityRecord() { }

        /// <summary>Gets or initializes the type of the request.</summary>
        public required string RequestType { get; init; }

        /// <summary>Gets or initializes a value indicating whether the success.</summary>
        public required bool Success { get; init; }

        /// <summary>Gets or initializes the message.</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>Gets or initializes the timestamp.</summary>
        public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    }

    /// <summary>Gets or sets the identifier of the client.</summary>
    public required string ClientId { get; set; }

    /// <summary>Gets or sets the name of the client.</summary>
    public required string ClientName { get; set; }

    /// <summary>Gets a list of names of the tenants.</summary>
    public HashSet<string> TenantNames { get; } = new();

    /// <summary>Gets or sets the registration.</summary>
    public SmartClientRegistration? Registration { get; set; } = null;

    /// <summary>Gets or sets the signing keys by algorithm.</summary>
    public Dictionary<string, Microsoft.IdentityModel.Tokens.SecurityKey> Keys { get; } = new();

    /// <summary>Gets the activity.</summary>
    public List<ClientActivityRecord> Activity { get; } = new();
}
