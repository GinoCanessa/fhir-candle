// <copyright file="ProviderConfiguration.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirServerHarness.Models;

/// <summary>A provider configuration.</summary>
public class ProviderConfiguration
{
    /// <summary>Values that represent FHIR versions.</summary>
    public enum FhirVersionCodes
    {
        /// <summary>FHIR R4.</summary>
        R4,

        /// <summary>FHIR R4B.</summary>
        R4B,

        /// <summary>FHIR R5.</summary>
        R5,
    }

    /// <summary>Information about the FHIR package.</summary>
    /// <param name="Id">      The identifier.</param>
    /// <param name="Version"> The version.</param>
    /// <param name="Registry">The registry.</param>
    public readonly record struct FhirPackageInfo(
        string Id,
        string Version,
        string Registry);

    /// <summary>Gets or sets the version.</summary>
    public required FhirVersionCodes FhirVersion { get; set; } = FhirVersionCodes.R4;

    /// <summary>Gets or sets the supported resources.</summary>
    public IEnumerable<string> SupportedResources { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the tenant route.</summary>
    public required string TenantRoute { get; set; } = string.Empty;

    /// <summary>Gets or sets the FHIR packages.</summary>
    public Dictionary<string, FhirPackageInfo> FhirPackages { get; } = new();
}
