// <copyright file="IFhirStoreManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Models;
using FhirStore.Storage;

namespace FhirServerHarness.Services;

/// <summary>Interface for FHIR store manager.</summary>
public interface IFhirStoreManager : IHostedService, IDisposable, IReadOnlyDictionary<string, IFhirStore>
{
    /// <summary>Occurs when On Changed.</summary>
    event EventHandler<EventArgs>? OnChanged;

    /// <summary>State has changed.</summary>
    void StateHasChanged();
}
