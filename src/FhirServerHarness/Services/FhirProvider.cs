// <copyright file="FhirProvider.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Services;
using Microsoft.AspNetCore.Http;
using FhirStore.Common.Models;

namespace FhirServerHarness.VersionedProvider.R4;

/// <summary>A FHIR provider.</summary>
public class FhirProvider : IVersionedProvider
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed;

    private Dictionary<string, Type> _fhirAndCsTypes = new();
    private Dictionary<Type, string> _csAndFhirTypes = new();
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private ProviderConfiguration _config;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>Gets or sets the configuration.</summary>
    public required ProviderConfiguration Configuration { get => _config; set => _config = value; }

    /// <summary>Gets a mapping to C# types by FHIR type.</summary>
    public Dictionary<string, Type> FhirAndCsTypes => _fhirAndCsTypes;

    /// <summary>Gets a mapping to FHIR type by C# type.</summary>
    public Dictionary<Type, string> CsAndFhirTypes => _csAndFhirTypes;

    /// <summary>Initializes this object.</summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <exception cref="ArgumentException">    Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <returns>An IVersionedProvider.</returns>
    public IVersionedProvider Init()
    {
        if (_config is null)
        {
            throw new ArgumentNullException(nameof(_config));
        }

        if (_config.FhirVersion != ProviderConfiguration.SupportedFhirVersions.R4B)
        {
            throw new ArgumentException($"Expected {Hl7.Fhir.Model.FHIRVersion.N4_1} but got {_config.FhirVersion}");
        }

        if (string.IsNullOrEmpty(_config.TenantRoute))
        {
            throw new ArgumentException("Tenant route cannot be null or empty");
        }

        //if (_config.SupportedResources?.Any() ?? false)
        //{
        //    foreach (string resource in _config.SupportedResources)
        //    {
        //        Type type = Hl7.Fhir.Model.ModelInfo.GetTypeForFhirType(resource);

        //        if (type is not null)
        //        {
        //            _fhirAndCsTypes.Add(resource, type);
        //            _csAndFhirTypes.Add(type, resource);
        //        }
        //    }
        //}
        //else
        //{
        //    foreach ((Type csType, string fhirType) in Hl7.Fhir.Model.ModelInfo.FhirCsTypeToString)
        //    {
        //        _fhirAndCsTypes.Add(fhirType, csType);
        //        _csAndFhirTypes.Add(csType, fhirType);
        //    }
        //}

        // TODO: process packages

        return this;
    }

    ///// <summary>Process the request asynchronous described by context.</summary>
    ///// <param name="context">The context.</param>
    ///// <returns>An asynchronous result.</returns>
    //public async Task ProcessRequestAsync(HttpContext context)
    //{
    //}


    /// <summary>
    /// Releases the unmanaged resources used by the
    /// FhirModelComparer.Server.Services.FhirManagerService and optionally releases the managed
    /// resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _hasDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
