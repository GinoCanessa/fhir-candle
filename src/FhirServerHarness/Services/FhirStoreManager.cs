// <copyright file="FhirStoreManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias storeR4;
extern alias storeR4B;
extern alias storeR5;

using System.Collections;
using FhirStore.Models;
using FhirStore.Storage;

namespace FhirServerHarness.Services;

/// <summary>Manager for FHIR stores.</summary>
public class FhirStoreManager : IFhirStoreManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<EventArgs>? OnChanged;

    private Dictionary<string, IFhirStore> _storesByController = new(StringComparer.OrdinalIgnoreCase);

    IEnumerable<string> IReadOnlyDictionary<string, IFhirStore>.Keys => _storesByController.Keys;

    IEnumerable<IFhirStore> IReadOnlyDictionary<string, IFhirStore>.Values => _storesByController.Values;

    int IReadOnlyCollection<KeyValuePair<string, IFhirStore>>.Count => _storesByController.Count;

    IFhirStore IReadOnlyDictionary<string, IFhirStore>.this[string key] => _storesByController[key];

    bool IReadOnlyDictionary<string, IFhirStore>.ContainsKey(string key) => _storesByController.ContainsKey(key);

    bool IReadOnlyDictionary<string, IFhirStore>.TryGetValue(string key, out IFhirStore value) => _storesByController.TryGetValue(key, out value!);

    IEnumerator<KeyValuePair<string, IFhirStore>> IEnumerable<KeyValuePair<string, IFhirStore>>.GetEnumerator() => _storesByController.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)_storesByController.GetEnumerator();


    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreManager"/> class.
    /// </summary>
    /// <param name="tenantConfigurations">The tenant configurations.</param>
    public FhirStoreManager(IEnumerable<ProviderConfiguration> tenantConfigurations)
    {
        // initialize the requested fhir stores
        foreach (ProviderConfiguration config in tenantConfigurations)
        {
            if (_storesByController.ContainsKey(config.ControllerName))
            {
                throw new Exception($"Duplicate controller names configured!: {config.ControllerName}");
            }

            switch (config.FhirVersion)
            {
                case ProviderConfiguration.SupportedFhirVersions.R4:
                    _storesByController.Add(config.ControllerName, new storeR4::FhirStore.Storage.VersionedFhirStore());
                    break;

                case ProviderConfiguration.SupportedFhirVersions.R4B:
                    _storesByController.Add(config.ControllerName, new storeR4B::FhirStore.Storage.VersionedFhirStore());
                    break;

                case ProviderConfiguration.SupportedFhirVersions.R5:
                    _storesByController.Add(config.ControllerName, new storeR5::FhirStore.Storage.VersionedFhirStore());
                    break;
            }

            _storesByController[config.ControllerName].Init(config);
        }
    }


    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("In StartAsync...");
        return Task.CompletedTask;
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("In StopAsync...");
        return Task.CompletedTask;
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

    /// <summary>State has changed.</summary>
    public void StateHasChanged()
    {
        EventHandler<EventArgs>? handler = OnChanged;

        if (handler != null)
        {
            handler(this, new());
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
