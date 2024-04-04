// <copyright file="FhirStoreManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;
extern alias candleR4B;
extern alias candleR5;

using System.Collections;
using System.Linq;
using fhir.candle.Models;
using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirStore.Smart;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace fhir.candle.Services;

/// <summary>Manager for FHIR stores.</summary>
public class FhirStoreManager : IFhirStoreManager, IDisposable
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>True if is initialized, false if not.</summary>
    private bool _isInitialized = false;

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

    /// <summary>The additional pages by controller.</summary>
    private Dictionary<string, List<PackagePageInfo>> _additionalPagesByController = new();

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

    /// <summary>Gets the additional pages by tenant.</summary>
    public IReadOnlyDictionary<string, IQueryable<PackagePageInfo>> AdditionalPagesByTenant => _additionalPagesByController.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.AsQueryable());

    /// <summary>State has changed.</summary>
    public void StateHasChanged()
    {
        EventHandler<EventArgs>? handler = OnChanged;

        if (handler != null)
        {
            handler(this, new());
        }
    }

    /// <summary>Initializes this object.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        // make sure the package service has been initalized
        _packageService.Init();

        _logger.LogInformation("FhirStoreManager <<< Creating FHIR tenants...");

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

        string root =
            Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ??
            Environment.CurrentDirectory ??
            string.Empty;

        // check for loading packages
        if (_packageService.IsConfigured &&
            (_serverConfig.PublishedPackages.Any() || _serverConfig.CiPackages.Any()))
        {
            // look for a package supplemental directory
            string supplemental = string.IsNullOrEmpty(_serverConfig.SourceDirectory)
                ? Program.FindRelativeDir(root, "fhirData", false)
            : _serverConfig.SourceDirectory;

            LoadRequestedPackages(supplemental, _serverConfig.LoadPackageExamples == true).Wait();
        }

        // sort through RI info
        if (!string.IsNullOrEmpty(_serverConfig.ReferenceImplementation))
        {
            // look for a package supplemental directory
            string supplemental = string.IsNullOrEmpty(_serverConfig.SourceDirectory)
                ? Program.FindRelativeDir(root, Path.Combine("fhirData", _serverConfig.ReferenceImplementation), false)
                : Path.Combine(_serverConfig.SourceDirectory, _serverConfig.ReferenceImplementation);

            LoadRiContents(supplemental);
        }

        // load packages
        LoadPackagePages();
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting FhirStoreManager...");

        Init();

        return Task.CompletedTask;
    }

    /// <summary>Loads package pages.</summary>
    /// <param name="manager">The manager.</param>
    /// <returns>The package pages.</returns>
    private void LoadPackagePages()
    {
        _logger.LogInformation("FhirStoreManager <<< Discovering package-based pages...");

        // get all page types
        List<PackagePageInfo> pages = new();

        //pages.AddRange(System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
        //    .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
        //    .Select(pt => new PackagePageInfo()
        //    {
        //        ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
        //        PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
        //        Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
        //        RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
        //        FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
        //        FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
        //        OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
        //    }));

        pages.AddRange(typeof(fhir.candle.Pages.RI.subscriptions.Tour).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        pages.AddRange(typeof(FhirCandle.Ui.R4.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        pages.AddRange(typeof(FhirCandle.Ui.R4B.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        pages.AddRange(typeof(FhirCandle.Ui.R5.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IPackagePage)))
            .Select(pt => new PackagePageInfo()
            {
                ContentFor = pt.GetProperty("ContentFor", typeof(string))?.GetValue(null) as string ?? string.Empty,
                PageName = pt.GetProperty("PageName", typeof(string))?.GetValue(null) as string ?? string.Empty,
                Description = pt.GetProperty("Description", typeof(string))?.GetValue(null) as string ?? string.Empty,
                RoutePath = pt.GetProperty("RoutePath", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionLiteral = pt.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null) as string ?? string.Empty,
                FhirVersionNumeric = pt.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null) as string ?? string.Empty,
                OnlyShowOnEndpoint = pt.GetProperty("OnlyShowOnEndpoint", typeof(string))?.GetValue(null) as string ?? string.Empty,
            }));

        _additionalPagesByController = new();
        foreach (string tenant in _tenants.Keys)
        {
            _additionalPagesByController.Add(tenant, new List<PackagePageInfo>());
        }

        // traverse page types to build package info
        foreach (PackagePageInfo page in pages)
        {
            Console.WriteLine($"Package page: {page.PageName}, FhirVersion: {page.FhirVersionLiteral} ({page.FhirVersionNumeric}), ContentFor: {page.ContentFor}, OnlyOnEndpoint: {page.OnlyShowOnEndpoint}");

            if (string.IsNullOrEmpty(page.FhirVersionLiteral))
            {
                foreach ((string name, IFhirStore store) in _storesByController)
                {
                    if (store.LoadedPackages.Contains(page.ContentFor) || store.LoadedSupplements.Contains(page.ContentFor))
                    {
                        Console.WriteLine($"Testing page: {page.PageName} (only for: {page.OnlyShowOnEndpoint}) against store {name}");

                        if (string.IsNullOrEmpty(page.OnlyShowOnEndpoint) || page.OnlyShowOnEndpoint.Equals(name, StringComparison.OrdinalIgnoreCase))
                        {
                            _additionalPagesByController[name].Add(page);
                        }
                        else
                        {
                            Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no endpoint match");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no content match");
                    }
                }

                continue;
            }

            if (!page.FhirVersionLiteral.TryFhirEnum(out TenantConfiguration.SupportedFhirVersions pageFhirVersion))
            {
                continue;
            }

            // traverse stores to marry contents
            foreach ((string name, IFhirStore store) in _storesByController)
            {
                if ((store.Config.FhirVersion == pageFhirVersion) &&
                    (store.LoadedPackages.Contains(page.ContentFor) || store.LoadedSupplements.Contains(page.ContentFor)))
                {
                    Console.WriteLine($"Testing page: {page.PageName} (only for: {page.OnlyShowOnEndpoint}) against store {name}");

                    if (string.IsNullOrEmpty(page.OnlyShowOnEndpoint) || page.OnlyShowOnEndpoint.Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        _additionalPagesByController[name].Add(page);
                    }
                    else
                    {
                        Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no endpoint match");
                    }
                }
                else
                {
                    Console.WriteLine($"Skipping page: {page.PageName} against store {name} - no FHIR version or content match");
                }
            }
        }
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

        _logger.LogInformation("FhirStoreManager <<< Loading RI contents...");

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
                    else if (Directory.Exists(Path.Combine(dir, tenantName)))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, tenantName),
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
                    else if (Directory.Exists(Path.Combine(dir, tenantName)))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, tenantName),
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
                    else if (Directory.Exists(Path.Combine(dir, tenantName)))
                    {
                        _storesByController[tenantName].LoadPackage(
                            string.Empty,
                            string.Empty,
                            Path.Combine(dir, tenantName),
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
        _logger.LogInformation("FhirStoreManager <<< loading requested packages...");

        // check for requested packages
        int waitCount = 0;
        while (!_packageService.IsReady)
        {
            _logger.LogInformation("FhirStoreManager <<< Waiting for package service...");

            await Task.Delay(100);
            waitCount++;

            if (waitCount > 200)
            {
                throw new Exception("Package service is not responding!");
            }
        }

        List<LoadedPackageRec> loadRecs = new();

        foreach (string branchName in _serverConfig.CiPackages)
        {
            _logger.LogInformation($"FhirStoreManager <<< loading CI package {branchName}...");

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
            _logger.LogInformation($"FhirStoreManager <<< Loading published package {directive}...");

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
            _logger.LogInformation($"FhirStoreManager <<< discovering and loading additional content for {r.directive}...");

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
                            else if ((!string.IsNullOrEmpty(r.supplementDirectory)) &&
                                Directory.Exists(Path.Combine(r.supplementDirectory, tenantName)))
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive,
                                    r.directory,
                                    Path.Combine(r.supplementDirectory, tenantName),
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
                            else if ((!string.IsNullOrEmpty(r.supplementDirectory)) &&
                                Directory.Exists(Path.Combine(r.supplementDirectory, tenantName)))
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive,
                                    r.directory,
                                    Path.Combine(r.supplementDirectory, tenantName),
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
                            else if ((!string.IsNullOrEmpty(r.supplementDirectory)) &&
                                Directory.Exists(Path.Combine(r.supplementDirectory, tenantName)))
                            {
                                _storesByController[tenantName].LoadPackage(
                                    r.directive,
                                    r.directory,
                                    Path.Combine(r.supplementDirectory, tenantName),
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
