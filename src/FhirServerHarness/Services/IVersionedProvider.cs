// <copyright file="IVersionedProvider.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Common.Models;
using Microsoft.AspNetCore.Http;

namespace FhirServerHarness.Services;

/// <summary>Interface for versioned FHIR provider.</summary>
public interface IVersionedProvider : IDisposable
{
    /// <summary>Initializes this object.</summary>
    /// <returns>An IVersionedProvider.</returns>
    IVersionedProvider Init();

    /// <summary>Gets a mapping to C# types by FHIR type.</summary>
    Dictionary<string, Type> FhirAndCsTypes { get; }

    /// <summary>Gets a mapping to FHIR type by C# type.</summary>
    Dictionary<Type, string> CsAndFhirTypes { get; }

    /// <summary>Gets or sets the configuration.</summary>
    ProviderConfiguration Configuration { get; set; }
}
