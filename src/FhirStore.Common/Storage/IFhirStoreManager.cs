// <copyright file="IFhirStoreManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Microsoft.Extensions.Hosting;

namespace FhirCandle.Storage;

/// <summary>Interface for FHIR store manager.</summary>
public interface IFhirStoreManager : IHostedService, IReadOnlyDictionary<string, IFhirStore>
{
    /// <summary>Occurs when On Changed.</summary>
    event EventHandler<EventArgs>? OnChanged;

    /// <summary>Loads requested packages.</summary>
    /// <returns>An asynchronous result.</returns>
    Task LoadRequestedPackages();

    /// <summary>State has changed.</summary>
    void StateHasChanged();
}
