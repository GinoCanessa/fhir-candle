// <copyright file="IFhirPackageService.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace fhir.candle.Services;

/// <summary>Interface for FHIR package service.</summary>
public interface IFhirPackageService : IHostedService
{
    /// <summary>Occurs when On Changed.</summary>
    event EventHandler<EventArgs>? OnChanged;

    /// <summary>Deletes the package described by packageDirective.</summary>
    /// <param name="packageDirective">The package directive.</param>
    void DeletePackage(string packageDirective);

    /// <summary>Attempts to find locally or download a given package.</summary>
    /// <param name="directive">  The directive.</param>
    /// <param name="directory">  [out] Pathname of the directory.</param>
    /// <param name="offlineMode">True to enable offline mode, false to disable it.</param>
    /// <param name="branchName"> Name of the branch.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool FindOrDownload(
        string directive,
        out string directory,
        bool offlineMode,
        string branchName);

    /// <summary>Initializes the FhirPackageService to a specific cache directory.</summary>
    /// <param name="cacheDirectory">Pathname of the cache directory.</param>
    void Init(string cacheDirectory);

    /// <summary>State has changed.</summary>
    void StateHasChanged();
}
