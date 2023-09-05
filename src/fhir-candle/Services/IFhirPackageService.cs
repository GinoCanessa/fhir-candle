// <copyright file="IFhirPackageService.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using static fhir.candle.Services.FhirPackageService;

namespace fhir.candle.Services;

/// <summary>Interface for FHIR package service.</summary>
public interface IFhirPackageService : IHostedService
{
    /// <summary>Occurs when On Changed.</summary>
    event EventHandler<EventArgs>? OnChanged;

    /// <summary>Gets a value indicating whether this service is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Gets a value indicating whether the package service is ready.</summary>
    bool IsReady { get; }

    /// <summary>Deletes the package described by packageDirective.</summary>
    /// <param name="packageDirective">The package directive.</param>
    void DeletePackage(string packageDirective);

    /// <summary>Attempts to find locally or download a given package.</summary>
    /// <param name="directive">        The directive.</param>
    /// <param name="branchName">       Name of the branch.</param>
    /// <param name="directory">        [out] Pathname of the directory.</param>
    /// <param name="fhirVersion">      [out] The FHIR version.</param>
    /// <param name="resolvedDirective">[out] The resolved directive.</param>
    /// <param name="offlineMode">      True to enable offline mode, false to disable it.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool FindOrDownload(
        string directive,
        string branchName,
        out IEnumerable<PackageCacheEntry> packages,
        bool offlineMode);

    /// <summary>Initializes the FhirPackageService.</summary>
    void Init();

    /// <summary>State has changed.</summary>
    void StateHasChanged();
}
