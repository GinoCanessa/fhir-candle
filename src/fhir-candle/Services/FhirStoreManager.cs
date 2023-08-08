// <copyright file="FhirStoreManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Collections;
using fhir.candle.Models;
using FhirCandle.Models;
using FhirCandle.Storage;

namespace fhir.candle.Services;

/// <summary>Manager for FHIR stores.</summary>
public class FhirStoreManager : IFhirStoreManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>The tenants.</summary>
    private Dictionary<string, TenantConfiguration> _tenants;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<EventArgs>? OnChanged;

    ///// <summary>The services.</summary>
    //private IEnumerable<IHostedService> _services;

    /// <summary>The stores by controller.</summary>
    private Dictionary<string, IFhirStore> _storesByController = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets an enumerable collection that contains the keys in the read-only dictionary.
    /// </summary>
    /// <typeparam name="string">    Type of the string.</typeparam>
    /// <typeparam name="IFhirStore">Type of the FHIR store.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, IFhirStore>.Keys => _storesByController.Keys;

    /// <summary>
    /// Gets an enumerable collection that contains the values in the read-only dictionary.
    /// </summary>
    /// <typeparam name="string">    Type of the string.</typeparam>
    /// <typeparam name="IFhirStore">Type of the FHIR store.</typeparam>
    IEnumerable<IFhirStore> IReadOnlyDictionary<string, IFhirStore>.Values => _storesByController.Values;

    /// <summary>Gets the number of elements in the collection.</summary>
    /// <typeparam name="string">     Type of the string.</typeparam>
    /// <typeparam name="IFhirStore>">Type of the FHIR store></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, IFhirStore>>.Count => _storesByController.Count;

    /// <summary>Gets the element that has the specified key in the read-only dictionary.</summary>
    /// <typeparam name="string">    Type of the string.</typeparam>
    /// <typeparam name="IFhirStore">Type of the FHIR store.</typeparam>
    /// <param name="key">The key to locate.</param>
    /// <returns>The element that has the specified key in the read-only dictionary.</returns>
    IFhirStore IReadOnlyDictionary<string, IFhirStore>.this[string key] => _storesByController[key];

    /// <summary>
    /// Determines whether the read-only dictionary contains an element that has the specified key.
    /// </summary>
    /// <typeparam name="string">    Type of the string.</typeparam>
    /// <typeparam name="IFhirStore">Type of the FHIR store.</typeparam>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the read-only dictionary contains an element that has the
    /// specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IFhirStore>.ContainsKey(string key) => _storesByController.ContainsKey(key);

    /// <summary>Gets the value that is associated with the specified key.</summary>
    /// <typeparam name="string">    Type of the string.</typeparam>
    /// <typeparam name="IFhirStore">Type of the FHIR store.</typeparam>
    /// <param name="key">  The key to locate.</param>
    /// <param name="value">[out] When this method returns, the value associated with the specified
    ///  key, if the key is found; otherwise, the default value for the type of the <paramref name="value" />
    ///  parameter. This parameter is passed uninitialized.</param>
    /// <returns>
    /// <see langword="true" /> if the object that implements the <see cref="T:System.Collections.Generic.IReadOnlyDictionary`2" />
    /// interface contains an element that has the specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IFhirStore>.TryGetValue(string key, out IFhirStore value) => _storesByController.TryGetValue(key, out value!);

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <typeparam name="string">     Type of the string.</typeparam>
    /// <typeparam name="IFhirStore>">Type of the FHIR store></typeparam>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator<KeyValuePair<string, IFhirStore>> IEnumerable<KeyValuePair<string, IFhirStore>>.GetEnumerator() => _storesByController.GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through
    /// the collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)_storesByController.GetEnumerator();

    /// <summary>Initializes a new instance of the <see cref="FhirStoreManager"/> class.</summary>
    /// <param name="tenants">The tenants.</param>
    /// <param name="logger"> The logger.</param>
    public FhirStoreManager(
        Dictionary<string, TenantConfiguration> tenants,
        ILogger<FhirStoreManager> logger)
    {
        _tenants = tenants;
        _logger = logger;
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

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting FhirStoreManager...");

        // initialize the requested fhir stores
        foreach ((string name, TenantConfiguration config) in _tenants)
        {
            if (_storesByController.ContainsKey(config.ControllerName))
            {
                throw new Exception($"Duplicate controller names configured!: {config.ControllerName}");
            }

            switch (config.FhirVersion)
            {
                case TenantConfiguration.SupportedFhirVersions.R4:
                    _storesByController.Add(name, new candleR4::FhirCandle.Storage.VersionedFhirStore());
                    break;

                case TenantConfiguration.SupportedFhirVersions.R4B:
                    _storesByController.Add(name, new candleR4B::FhirCandle.Storage.VersionedFhirStore());
                    break;

                case TenantConfiguration.SupportedFhirVersions.R5:
                    _storesByController.Add(name, new candleR5::FhirCandle.Storage.VersionedFhirStore());
                    break;
            }

            _storesByController[name].Init(config);
            //_storesByController[config.ControllerName].OnSubscriptionSendEvent += FhirStoreManager_OnSubscriptionSendEvent;
        }

        return Task.CompletedTask;
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
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

                //foreach (IFhirStore store in _storesByController.Values)
                //{
                //    store.OnSubscriptionSendEvent -= FhirStoreManager_OnSubscriptionSendEvent;
                //}
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
