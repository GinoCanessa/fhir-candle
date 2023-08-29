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
using MudBlazor;

namespace fhir.candle.Services;

/// <summary>Manager for FHIR stores.</summary>
public class FhirStoreManager : IFhirStoreManager, IDisposable
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>The tenants.</summary>
    private Dictionary<string, TenantConfiguration> _tenants;

    /// <summary>The server configuration.</summary>
    private ServerConfiguration _serverConfig;

    /// <summary>The package service.</summary>
    private IFhirPackageService _packageService;

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
    /// <param name="tenants">            The tenants.</param>
    /// <param name="logger">             The logger.</param>
    /// <param name="serverConfiguration">The server configuration.</param>
    /// <param name="fhirPackageService"> The FHIR package service.</param>
    public FhirStoreManager(
        Dictionary<string, TenantConfiguration> tenants,
        ILogger<FhirStoreManager> logger,
        ServerConfiguration serverConfiguration,
        IFhirPackageService fhirPackageService)
    {
        _tenants = tenants;
        _logger = logger;
        _serverConfig = serverConfiguration;
        _packageService = fhirPackageService;
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

    /// <summary>A loaded packagage.</summary>
    /// <param name="directive">          The directive.</param>
    /// <param name="name">               The name.</param>
    /// <param name="version">            The version.</param>
    /// <param name="directory">          Pathname of the directory.</param>
    /// <param name="supplementDirectory">Pathname of the supplement directory.</param>
    /// <param name="fhirVersion">        The FHIR version.</param>
    private record struct LoadedPackageRec(
        string directive,
        string name,
        string version,
        string directory,
        string supplementDirectory,
        FhirPackageService.FhirSequenceEnum fhirVersion
        );

    /// <summary>Loads ri contents.</summary>
    /// <param name="dir">The dir.</param>
    public void LoadRiContents(string dir)
    {
        if (string.IsNullOrEmpty(dir) ||
            !Directory.Exists(dir))
        {
            return;
        }

        // loop over controllers to see where we can add this
        foreach ((string tenantName, TenantConfiguration config) in _tenants)
        {
            switch (config.FhirVersion)
            {
                case TenantConfiguration.SupportedFhirVersions.R4:
                    if (Directory.Exists(Path.Combine(dir, "r4")))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, "r4"),
                            true);
                    }
                    else
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            dir,
                            true);
                    }
                    break;
                case TenantConfiguration.SupportedFhirVersions.R4B:
                    if (Directory.Exists(Path.Combine(dir, "r4b")))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, "r4b"),
                            true);
                    }
                    else
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            dir,
                            true);
                    }
                    break;
                case TenantConfiguration.SupportedFhirVersions.R5:
                    if (Directory.Exists(Path.Combine(dir, "r5")))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, "r5"),
                            true);
                    }
                    else
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            dir,
                            true);
                    }
                    break;
                default:
                    break;
            }
        }
    }

    /// <summary>Loads requested packages.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="supplementalRoot">The supplemental root.</param>
    /// <param name="loadExamples">    True to load examples.</param>
    /// <returns>An asynchronous result.</returns>
    public async Task LoadRequestedPackages(string supplementalRoot, bool loadExamples)
    {
        // check for requested packages
        int waitCount = 0;
        while (!_packageService.IsReady)
        {
            await Task.Delay(1000);
            waitCount++;

            if (waitCount > 20)
            {
                throw new Exception("Package service is not responding!");
            }
        }

        List<LoadedPackageRec> loadRecs = new();

        foreach (string branchName in _serverConfig.CiPackages)
        {
            if (!_packageService.FindOrDownload(string.Empty, branchName, out IEnumerable<FhirPackageService.PackageCacheEntry> pacakges, false))
            {
                throw new Exception($"Unable to find or download CI package: {branchName}");
            }

            List<LoadedPackageRec> directiveRecs = new();

            foreach (FhirPackageService.PackageCacheEntry entry in pacakges)
            {
                directiveRecs.Add(EntryToRec(supplementalRoot, entry));
            }

            // single entry means single package, multiple means first is umbrella package
            if (directiveRecs.Count == 1)
            {
                loadRecs.Add(directiveRecs[0]);
            }
            else
            {
                loadRecs.AddRange(directiveRecs.Skip(1));
            }
        }

        foreach (string directive in _serverConfig.PublishedPackages)
        {
            if (!_packageService.FindOrDownload(directive, string.Empty, out IEnumerable<FhirPackageService.PackageCacheEntry> pacakges, false))
            {
                throw new Exception($"Unable to find or download published package: {directive}");
            }

            List<LoadedPackageRec> directiveRecs = new();

            foreach (FhirPackageService.PackageCacheEntry entry in pacakges)
            {
                directiveRecs.Add(EntryToRec(supplementalRoot, entry));
            }

            // single entry means single package, multiple means first is umbrella package
            if (directiveRecs.Count == 1)
            {
                loadRecs.Add(directiveRecs[0]);
            }
            else
            {
                loadRecs.AddRange(directiveRecs.Skip(1));
            }
        }

        foreach (LoadedPackageRec r in loadRecs)
        {
            // loop over controllers to see where we can add this
            foreach ((string tenantName, TenantConfiguration config) in _tenants)
            {
                switch (config.FhirVersion)
                {
                    case TenantConfiguration.SupportedFhirVersions.R4:
                        if (r.fhirVersion == FhirPackageService.FhirSequenceEnum.R4)
                        {
                            if ((!string.IsNullOrEmpty(r.supplementDirectory)) &&
                                Directory.Exists(Path.Combine(r.supplementDirectory, "r4")))
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive, 
                                    r.directory, 
                                    Path.Combine(r.supplementDirectory, "r4"),
                                    loadExamples);
                            }
                            else
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive, 
                                    r.directory, 
                                    r.supplementDirectory,
                                    loadExamples);
                            }
                        }
                        break;
                    case TenantConfiguration.SupportedFhirVersions.R4B:
                        if (r.fhirVersion == FhirPackageService.FhirSequenceEnum.R4B)
                        {
                            if ((!string.IsNullOrEmpty(r.supplementDirectory)) &&
                                Directory.Exists(Path.Combine(r.supplementDirectory, "r4b")))
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive, 
                                    r.directory, 
                                    Path.Combine(r.supplementDirectory, "r4b"),
                                    loadExamples);
                            }
                            else
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive, 
                                    r.directory, 
                                    r.supplementDirectory,
                                    loadExamples);
                            }
                        }
                        break;
                    case TenantConfiguration.SupportedFhirVersions.R5:
                        if (r.fhirVersion == FhirPackageService.FhirSequenceEnum.R5)
                        {
                            if ((!string.IsNullOrEmpty(r.supplementDirectory)) &&
                                Directory.Exists(Path.Combine(r.supplementDirectory, "r5")))
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive, 
                                    r.directory, 
                                    Path.Combine(r.supplementDirectory, 
                                    "r5"),
                                    loadExamples);
                            }
                            else
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive, 
                                    r.directory, 
                                    r.supplementDirectory, 
                                    loadExamples);
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        LoadedPackageRec EntryToRec(string supplementalRoot, FhirPackageService.PackageCacheEntry entry)
        {
            string supplementDir = GetSupplementDir(supplementalRoot, entry);

            string[] directiveComponents = entry.resolvedDirective.Split('#');

            string packageName = directiveComponents.Any() ? directiveComponents[0] : string.Empty;
            string packageVersion = directiveComponents.Length > 1 ? directiveComponents[1] : string.Empty;

            return new LoadedPackageRec(
                entry.resolvedDirective,
                packageName,
                packageVersion,
                entry.directory,
                supplementDir,
                entry.fhirVersion);
        }

        //bool ShouldLoadPackage(
        //    LoadedPackageRec r,
        //    TenantConfiguration t,
        //    out string directive,
        //    out string directory,
        //    out string supplementalDirectory)
        //{


        //    if (!VersionsMatch(r, t))
        //    {
        //        // check to see if we have a non-matching supplemental package

        //        directive = string.Empty;
        //        directory = string.Empty;
        //        supplementalDirectory = string.Empty;
        //        return false;
        //    }


        //}

    }

    /// <summary>Versions match.</summary>
    /// <param name="r">A LoadedPackageRec to process.</param>
    /// <param name="t">A TenantConfiguration to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    static bool VersionsMatch(LoadedPackageRec r, TenantConfiguration t) => t.FhirVersion switch
    {
        TenantConfiguration.SupportedFhirVersions.R4 => r.fhirVersion == FhirPackageService.FhirSequenceEnum.R4,
        TenantConfiguration.SupportedFhirVersions.R4B => r.fhirVersion == FhirPackageService.FhirSequenceEnum.R4B,
        TenantConfiguration.SupportedFhirVersions.R5 => r.fhirVersion == FhirPackageService.FhirSequenceEnum.R5,
        _ => false,
    };

    /// <summary>Gets supplement dir.</summary>
    /// <param name="supplementalRoot"> The supplemental root.</param>
    /// <param name="resolvedDirective">The resolved directive.</param>
    /// <returns>The supplement dir.</returns>
    private string GetSupplementDir(string supplementalRoot, FhirPackageService.PackageCacheEntry entry)
    {
        if (string.IsNullOrEmpty(supplementalRoot))
        {
            return string.Empty;
        }

        string dir;

        // check to see if we have an exact match
        dir = Path.Combine(supplementalRoot, entry.resolvedDirective);
        if (Directory.Exists(dir))
        {
            return dir;
        }

        // check for named package without version
        dir = Path.Combine(supplementalRoot, entry.name);
        if (Directory.Exists(dir))
        {
            return dir;
        }

        // check for umbrella package with version
        dir = Path.Combine(supplementalRoot, entry.umbrellaPackageName + "#" + entry.version);
        if (Directory.Exists(dir))
        {
            return dir;
        }

        // check for umbrella package without version
        dir = Path.Combine(supplementalRoot, entry.umbrellaPackageName);
        if (Directory.Exists(dir))
        {
            return dir;
        }

        return string.Empty;
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
