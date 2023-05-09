// <copyright file="IResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Common.Models;
using FhirStore.Models;

namespace FhirStore.Common.Storage;

/// <summary>Interface for resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public interface IResourceStore : IDisposable, IReadOnlyDictionary<string, object>
{
    /// <summary>Occurs when On Changed.</summary>
    event EventHandler<EventArgs>? OnChanged;

    /// <summary>State has changed.</summary>
    void StateHasChanged();

    ///// <summary>Reads a specific instance of a resource.</summary>
    ///// <param name="id">[out] The identifier.</param>
    ///// <returns>The requested resource or null.</returns>
    //object? InstanceRead(string id);

    ///// <summary>Create an instance of a resource.</summary>
    ///// <param name="source">         [out] The resource.</param>
    ///// <param name="allowExistingId">True to allow, false to suppress the existing identifier.</param>
    ///// <returns>The created resource, or null if it could not be created.</returns>
    //object? InstanceCreate(object source, bool allowExistingId);

    ///// <summary>Update a specific instance of a resource.</summary>
    ///// <param name="source">     [out] The resource.</param>
    ///// <param name="allowCreate">True to allow, false to suppress the create.</param>
    ///// <returns>The updated resource, or null if it could not be performed.</returns>
    //object? InstanceUpdate(object source, bool allowCreate);

    ///// <summary>Instance delete.</summary>
    ///// <param name="id">[out] The identifier.</param>
    ///// <returns>The deleted resource or null.</returns>
    //object? InstanceDelete(string id);

    ///// <summary>Performs a type search in this resource store.</summary>
    ///// <param name="parameters">The query.</param>
    ///// <returns>
    ///// An enumerator that allows foreach to be used to process type search in this collection.
    ///// </returns>
    //IEnumerable<object>? TypeSearch(IEnumerable<object> parameters);

    ///// <summary>Gets the search includes supported by this store.</summary>
    ///// <returns>
    ///// An enumerator that allows foreach to be used to process the search includes in this
    ///// collection.
    ///// </returns>
    //IEnumerable<string> GetSearchIncludes();

    ///// <summary>Gets the search reverse includes supported by this store.</summary>
    ///// <returns>
    ///// An enumerator that allows foreach to be used to process the search reverse includes in this
    ///// collection.
    ///// </returns>
    //IEnumerable<string> GetSearchRevIncludes();
}
