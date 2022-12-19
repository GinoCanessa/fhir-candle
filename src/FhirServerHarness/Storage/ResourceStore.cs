// <copyright file="ResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Model;

namespace FhirServerHarness.Storage;

/// <summary>A resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public class ResourceStore<T> : IResourceStore
    where T : Resource
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The resource store.</summary>
    private Dictionary<string, T> _resourceStore = new();

    /// <summary>Reads a specific instance of a resource.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The requested resource or null.</returns>
    public Resource? InstanceRead(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(id))
        {
            return null;
        }

        return _resourceStore[id];
    }

    /// <summary>Create an instance of a resource.</summary>
    /// <param name="source">[out] The resource.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    public Resource? InstanceCreate(Resource source)
    {
        if (source == null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(source.Id))
        {
            source.Id = Guid.NewGuid().ToString();
        }

        if (_resourceStore.ContainsKey(source.Id))
        {
            return null;
        }

        if (source is not T)
        {
            return null;
        }

        if (source.Meta == null)
        {
            source.Meta = new Meta();
        }

        source.Meta.VersionId = "1";
        source.Meta.LastUpdated = DateTimeOffset.UtcNow;

        _resourceStore.Add(source.Id, (T)source);
        return source;
    }

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">[out] The resource.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    public Resource? InstanceUpdate(Resource source)
    {
        if (string.IsNullOrEmpty(source?.Id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(source.Id))
        {
            return null;
        }

        if (source is not T)
        {
            return null;
        }

        if (int.TryParse(_resourceStore[source.Id].Meta.VersionId, out int version))
        {
            source.Meta.VersionId = (version + 1).ToString();
        }
        else
        {
            source.Meta.VersionId = "1";
        }

        source.Meta.LastUpdated = DateTimeOffset.UtcNow;

        _resourceStore[source.Id] = (T)source;
        return source;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The deleted resource or null.</returns>
    public Resource? InstanceDelete(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(id))
        {
            return null;
        }

        T instance = _resourceStore[id];
        _ = _resourceStore.Remove(id);
        return instance;
    }

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
