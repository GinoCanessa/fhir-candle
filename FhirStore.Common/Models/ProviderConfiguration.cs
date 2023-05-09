// <copyright file="ProviderConfiguration.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirStore.Common.Models;

/// <summary>A provider configuration.</summary>
public class ProviderConfiguration
{
    /// <summary>Values that represent supported FHIR versions.</summary>
    public enum SupportedFhirVersions
    {
        R4,
        R4B,
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
    public required SupportedFhirVersions FhirVersion { get; set; } = SupportedFhirVersions.R5;

    /// <summary>Gets or sets the supported resources.</summary>
    public IEnumerable<string> SupportedResources { get; set; } = Array.Empty<string>();

    /// <summary>Gets or sets the supported MIME formats.</summary>
    public IEnumerable<string> SupportedFormats { get; set; } = new string[]
    {
        "application/fhir+json",
        "application/fhir+xml",
    };

    /// <summary>Gets or sets route controller name.</summary>
    public required string ControllerName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute base url of this store.
    /// </summary>
    public required string BaseUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the FHIR packages.</summary>
    public Dictionary<string, FhirPackageInfo> FhirPackages { get; } = new();
}
