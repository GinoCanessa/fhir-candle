// <copyright file="IResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirServerHarness.Storage;

/// <summary>Interface for resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public interface IResourceStore : IDisposable
{
    /// <summary>Reads a specific instance of a resource.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The requested resource or null.</returns>
    Hl7.Fhir.Model.Resource? InstanceRead(string id);

    /// <summary>Create an instance of a resource.</summary>
    /// <param name="source">[out] The resource.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    Hl7.Fhir.Model.Resource? InstanceCreate(Hl7.Fhir.Model.Resource source);

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">[out] The resource.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    Hl7.Fhir.Model.Resource? InstanceUpdate(Hl7.Fhir.Model.Resource source);

    /// <summary>Instance delete.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The deleted resource or null.</returns>
    Hl7.Fhir.Model.Resource? InstanceDelete(string id);
}
