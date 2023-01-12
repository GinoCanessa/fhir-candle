// <copyright file="IResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.Model;

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
    /// <param name="source">         [out] The resource.</param>
    /// <param name="allowExistingId">True to allow, false to suppress the existing identifier.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    Hl7.Fhir.Model.Resource? InstanceCreate(Hl7.Fhir.Model.Resource source, bool allowExistingId);

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">[out] The resource.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    Hl7.Fhir.Model.Resource? InstanceUpdate(Hl7.Fhir.Model.Resource source);

    /// <summary>Instance delete.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The deleted resource or null.</returns>
    Hl7.Fhir.Model.Resource? InstanceDelete(string id);

    /// <summary>Performs a type search in this resource store.</summary>
    /// <param name="parameters">The query.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process type search in this collection.
    /// </returns>
    IEnumerable<Hl7.Fhir.Model.Resource>? TypeSearch(IEnumerable<ParsedSearchParameter> parameters);

    /// <summary>Adds a search parameter definition.</summary>
    /// <param name="spDefinition">The sp definition.</param>
    void AddSearchParameterDefinition(ModelInfo.SearchParamDefinition spDefinition);

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetSearchParamDefinition(string name, out ModelInfo.SearchParamDefinition? spDefinition);

}
