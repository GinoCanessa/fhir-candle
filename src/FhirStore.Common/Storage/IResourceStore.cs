// <copyright file="IResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;

namespace FhirCandle.Storage;

/// <summary>Interface for resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public interface IResourceStore : IDisposable, IReadOnlyDictionary<string, object>
{
    /// <summary>Occurs when On Instance Created.</summary>
    event EventHandler<StoreInstanceEventArgs>? OnInstanceCreated;

    /// <summary>Occurs when On Instance Updated.</summary>
    event EventHandler<StoreInstanceEventArgs>? OnInstanceUpdated;

    /// <summary>Occurs when On Instance Deleted.</summary>
    event EventHandler<StoreInstanceEventArgs>? OnInstanceDeleted;

    /// <summary>Registers the instance created.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    void RegisterInstanceCreated(string resourceId);

    /// <summary>Registers the instance updated.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    void RegisterInstanceUpdated(string resourceId);

    /// <summary>Registers the instance deleted.</summary>
    /// <param name="resourceId">Identifier for the resource.</param>
    void RegisterInstanceDeleted(string resourceId);

    /// <summary>Gets a value indicating whether this resource store contains conformance resources.</summary>
    bool ResourcesAreConformance { get; }

    /// <summary>Gets a value indicating whether the resources have an identifier that is a List of identifiers.</summary>
    bool ResourcesAreIdentifiable { get; }

    /// <summary>Gets a value indicating whether the resources have name.</summary>
    bool ResourcesHaveName { get; }

    /// <summary>Gets the instance table view.</summary>
    /// <returns>The instance table view.</returns>
    IQueryable<InstanceTableRec> GetInstanceTableView();
}
