// <copyright file="FhirStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Search;
using FhirStore.Models;
using FhirStore.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Net;
using FhirStore.Versioned.Shims.Subscriptions;
using System.Text.RegularExpressions;
using static fhir.candle.Search.SearchDefinitions;
using System.Collections;
using System.Collections.Concurrent;
using FhirStore.Operations;
using System.Net.Mime;

namespace FhirStore.Storage;

/// <summary>A FHIR store.</summary>
public partial class VersionedFhirStore : IFhirStore
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<EventArgs>? OnChanged;

    /// <summary>Occurs when a Subscription or SubscriptionTopic resource has changed.</summary>
    public event EventHandler<SubscriptionChangedEventArgs>? OnSubscriptionsChanged;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<SubscriptionSendEventArgs>? OnSubscriptionSendEvent;

    /// <summary>Occurs when a received subscription has changed.</summary>
    public event EventHandler<ReceivedSubscriptionChangedEventArgs>? OnReceivedSubscriptionChanged;

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<ReceivedSubscriptionEventArgs>? OnReceivedSubscriptionEvent;

    /// <summary>The compiler.</summary>
    private static FhirPathCompiler _compiler = null!;

    /// <summary>The JSON parser.</summary>
    FhirJsonPocoDeserializer _jsonParser = new(new FhirJsonPocoDeserializerSettings()
    {
        DisableBase64Decoding = false,
    });

    /// <summary>The JSON serializer for full resources.</summary>
    FhirJsonPocoSerializer _jsonSerializerFull = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = null,
    });

    /// <summary>The JSON serializer for summary=data.</summary>
    FhirJsonPocoSerializer _jsonSerializerData = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = SerializationFilter.ForText(),
    });

    /// <summary>The JSON serializer for summary=text.</summary>
    FhirJsonPocoSerializer _jsonSerializerText = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = SerializationFilter.ForData(),
    });

    /// <summary>The JSON serializer for summary=true.</summary>
    FhirJsonPocoSerializer _jsonSerializerSummary = new(new FhirJsonPocoSerializerSettings()
    {
        SummaryFilter = SerializationFilter.ForSummary(),
    });

    /// <summary>The XML parser.</summary>
    FhirXmlPocoDeserializer _xmlParser = new(new FhirXmlPocoDeserializerSettings()
    {
        DisableBase64Decoding = false,
    });

    /// <summary>The XML serializer.</summary>
    FhirXmlPocoSerializer _xmlSerializer = new();

    /// <summary>The store.</summary>
    private Dictionary<string, IVersionedResourceStore> _store = new();

    /// <summary>The search tester.</summary>
    private SearchTester _searchTester;

    /// <summary>Gets the supported resources.</summary>
    public IEnumerable<string> SupportedResources => _store.Keys.ToArray();

    /// <summary>(Immutable) The cache of compiled search parameter extraction functions.</summary>
    private readonly ConcurrentDictionary<string, CompiledExpression> _compiledSearchParameters = new();

    /// <summary>The sp lock object.</summary>
    private object _spLockObject = new();

    /// <summary>The subscription topic converter.</summary>
    internal static TopicConverter _topicConverter = new();
    
    /// <summary>The subscription converter.</summary>
    internal static SubscriptionConverter _subscriptionConverter = new();

    /// <summary>(Immutable) The topics, by id.</summary>
    internal readonly ConcurrentDictionary<string, ParsedSubscriptionTopic> _topics = new();

    /// <summary>(Immutable) The subscriptions, by id.</summary>
    internal readonly ConcurrentDictionary<string, ParsedSubscription> _subscriptions = new();

    /// <summary>(Immutable) The fhirpath variable matcher.</summary>
    [GeneratedRegex("[%][\\w\\-]+", RegexOptions.Compiled)]
    private static partial Regex _fhirpathVarMatcher();

    /// <summary>The configuration.</summary>
    private TenantConfiguration _config = null!;

    /// <summary>True if capabilities are stale.</summary>
    private bool _capabilitiesAreStale = true;

    /// <summary>(Immutable) Identifier for the capability statement.</summary>
    private const string _capabilityStatementId = "metadata";

    /// <summary>The operations supported by this server, by name.</summary>
    private Dictionary<string, IFhirOperation> _operations = new();

    /// <summary>True while the store is loading initial content.</summary>
    private bool _inLoad = false;

    /// <summary>Items to reprocess after a load completes.</summary>
    private Dictionary<string, List<object>>? _loadReprocess = null;

    /// <summary>Number of maximum resources.</summary>
    private int _maxResourceCount = 0;

    /// <summary>Queue of identifiers of resources (used for max resource cleaning).</summary>
    private ConcurrentQueue<string> _resourceQ = new();

    /// <summary>The received notifications.</summary>
    private ConcurrentDictionary<string, List<ParsedSubscriptionStatus>> _receivedNotifications = new();

    /// <summary>(Immutable) The received notification window ticks.</summary>
    private static readonly long _receivedNotificationWindowTicks = TimeSpan.FromMinutes(10).Ticks;

    /// <summary>True if this store has protected content.</summary>
    private bool _hasProtected = false;

    /// <summary>List of identifiers for the protected.</summary>
    private HashSet<string> _protectedResources = new();

    /// <summary>The storage capacity timer.</summary>
    private System.Threading.Timer? _capacityMonitor = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedFhirStore"/> class.
    /// </summary>
    public VersionedFhirStore()
    {
        _searchTester = new() { FhirStore = this, };
    }

    /// <summary>Initializes this object.</summary>
    /// <param name="config">The configuration.</param>
    public void Init(TenantConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrEmpty(config.ControllerName))
        {
            throw new ArgumentNullException(nameof(config.ControllerName));
        }

        if (string.IsNullOrEmpty(config.BaseUrl))
        {
            throw new ArgumentNullException(nameof(config.BaseUrl));
        }

        _config = config;
        //_baseUri = new Uri(config.ControllerName);

        SymbolTable st = new SymbolTable().AddStandardFP().AddFhirExtensions();
        _compiler = new(st);

        Type rsType = typeof(ResourceStore<>);

        // traverse known resource types to create individual resource stores
        foreach ((string tn, Type t) in ModelInfo.FhirTypeToCsType)
        {
            // skip non-resources
            if (!ModelInfo.IsKnownResource(tn))
            {
                continue;
            }

            // skip resources we do not store (per spec)
            switch (tn)
            {
                case "Parameters":
                case "OperationOutcome":
                case "SubscriptionStatus":
                    continue;
            }

            Type[] tArgs = { t };

            IVersionedResourceStore? irs = (IVersionedResourceStore?)Activator.CreateInstance(
                rsType.MakeGenericType(tArgs),
                this,
                _searchTester,
                _topicConverter,
                _subscriptionConverter);

            if (irs != null)
            {
                _store.Add(tn, irs);
                irs.OnChanged += ResourceStore_OnChanged;
            }
        }

        // create executable versions of known search parameters
        foreach (ModelInfo.SearchParamDefinition spDefinition in ModelInfo.SearchParameters)
        {
            if (spDefinition.Resource != null)
            {
                if (_store.TryGetValue(spDefinition.Resource, out IVersionedResourceStore? rs))
                {
                    rs.SetExecutableSearchParameter(spDefinition);
                }
            }
        }

        // check for a load directory
        if (config.LoadDirectory != null)
        {
            _hasProtected = config.ProtectLoadedContent;
            _loadReprocess = new();
            _inLoad = true;

            string serializedResource, serializedOutcome, eTag, lastModified, location;
            HttpStatusCode sc;

            foreach (FileInfo file in config.LoadDirectory.GetFiles("*.*", SearchOption.AllDirectories))
            {
                switch (file.Extension.ToLowerInvariant())
                {
                    case ".json":
                        sc = InstanceCreate(
                            string.Empty,
                            File.ReadAllText(file.FullName),
                            "application/fhir+json",
                            "application/fhir+json",
                            false,
                            string.Empty,
                            true,
                            out serializedResource,
                            out serializedOutcome,
                            out eTag,
                            out lastModified,
                            out location);
                        break;

                    case ".xml":
                        sc = InstanceCreate(
                            string.Empty,
                            File.ReadAllText(file.FullName),
                            "application/fhir+xml",
                            "application/fhir+xml",
                            false,
                            string.Empty,
                            true,
                            out serializedResource,
                            out serializedOutcome,
                            out eTag,
                            out lastModified,
                            out location);
                        break;

                    default:
                        continue;
                }

                Console.WriteLine($"{config.ControllerName} <<< {sc}: {file.FullName}");
            }

            _inLoad = false;

            // reload any subscriptions in case they loaded before topics
            if (_loadReprocess.Any())
            {
                foreach ((string key, List<object> list) in _loadReprocess)
                {
                    switch (key)
                    {
                        case "Subscription":
                            {
                                foreach (object sub in list)
                                {
                                    _ = SetExecutableSubscription((ParsedSubscription)sub);
                                }
                            }
                            break;
                    }
                }
            }

            _loadReprocess = null;
        }

        // load operations for this fhir version
        IEnumerable<Type> operationTypes = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IFhirOperation)));

        foreach (Type opType in operationTypes)
        {
            IFhirOperation? fhirOp = (IFhirOperation?)Activator.CreateInstance(opType);

            if ((fhirOp == null) ||
                (!fhirOp.CanonicalByFhirVersion.ContainsKey(_config.FhirVersion)))
            {
                continue;
            }

            if (!_operations.ContainsKey(fhirOp.OperationName))
            {
                _operations.Add(fhirOp.OperationName, fhirOp);
            }
        }

        // create a timer to check max resource count if we are monitoring that
        _maxResourceCount = config.MaxResourceCount;
        if (_maxResourceCount > 0)
        {
            _capacityMonitor = new System.Threading.Timer(
                CheckUsage,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>Gets the configuration.</summary>
    public TenantConfiguration Config => _config;

    /// <summary>Supports resource.</summary>
    /// <param name="resourceName">Name of the resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool SupportsResource(string resourceName) => _store.ContainsKey(resourceName);

    /// <summary>
    /// Gets an enumerable collection that contains the keys in the read-only dictionary.
    /// </summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, IResourceStore>.Keys => _store.Keys;

    /// <summary>
    /// Gets an enumerable collection that contains the values in the read-only dictionary.
    /// </summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    IEnumerable<IResourceStore> IReadOnlyDictionary<string, IResourceStore>.Values => _store.Values;

    /// <summary>Gets the number of elements in the collection.</summary>
    /// <typeparam name="string">         Type of the string.</typeparam>
    /// <typeparam name="IResourceStore>">Type of the resource store></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, IResourceStore>>.Count => _store.Count;

    /// <summary>Gets the current topics.</summary>
    public IEnumerable<ParsedSubscriptionTopic> CurrentTopics => _topics.Values;

    /// <summary>Gets the current subscriptions.</summary>
    public IEnumerable<ParsedSubscription> CurrentSubscriptions => _subscriptions.Values;

    /// <summary>Gets the received notifications.</summary>
    public IEnumerable<KeyValuePair<string, List<ParsedSubscriptionStatus>>> ReceivedNotifications => _receivedNotifications.AsEnumerable();

    /// <summary>Gets the element that has the specified key in the read-only dictionary.</summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    /// <param name="key">The key to locate.</param>
    /// <returns>The element that has the specified key in the read-only dictionary.</returns>
    IResourceStore IReadOnlyDictionary<string, IResourceStore>.this[string key] => _store[key];

    /// <summary>
    /// Determines whether the read-only dictionary contains an element that has the specified key.
    /// </summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    /// <param name="key">The key to locate.</param>
    /// <returns>
    /// <see langword="true" /> if the read-only dictionary contains an element that has the
    /// specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IResourceStore>.ContainsKey(string key) => _store.ContainsKey(key);

    /// <summary>Gets the value that is associated with the specified key.</summary>
    /// <typeparam name="string">        Type of the string.</typeparam>
    /// <typeparam name="IResourceStore">Type of the resource store.</typeparam>
    /// <param name="key">  The key to locate.</param>
    /// <param name="value">[out] When this method returns, the value associated with the specified
    ///  key, if the key is found; otherwise, the default value for the type of the <paramref name="value" />
    ///  parameter. This parameter is passed uninitialized.</param>
    /// <returns>
    /// <see langword="true" /> if the object that implements the <see cref="T:System.Collections.Generic.IReadOnlyDictionary`2" />
    /// interface contains an element that has the specified key; otherwise, <see langword="false" />.
    /// </returns>
    bool IReadOnlyDictionary<string, IResourceStore>.TryGetValue(string key, out IResourceStore value)
    {
        bool result = _store.TryGetValue(key, out IVersionedResourceStore? rStore);
        value = rStore ?? null!;
        return result;
    }

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <typeparam name="string">         Type of the string.</typeparam>
    /// <typeparam name="IResourceStore>">Type of the resource store></typeparam>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    IEnumerator<KeyValuePair<string, IResourceStore>> IEnumerable<KeyValuePair<string, IResourceStore>>.GetEnumerator() =>
        _store.Select(kvp => new KeyValuePair<string, IResourceStore>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>
    /// An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through
    /// the collection.
    /// </returns>
    IEnumerator IEnumerable.GetEnumerator() =>
        _store.Select(kvp => new KeyValuePair<string, IResourceStore>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Gets a compiled search parameter expression.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="name">        The sp name/code/id.</param>
    /// <param name="expression">  The FHIRPath expression.</param>
    /// <returns>The compiled.</returns>
    public CompiledExpression GetCompiledSearchParameter(string resourceType, string name, string expression)
    {
        string c = resourceType + "." + name;

        lock (_spLockObject)
        {
            if (!_compiledSearchParameters.ContainsKey(c))
            {
                _ = _compiledSearchParameters.TryAdd(c, _compiler.Compile(expression));
            }
        }

        return _compiledSearchParameters[c];
    }

    /// <summary>Check resource usage.</summary>
    private void CheckResourceUsage()
    {
        // check for total resources
        if ((_maxResourceCount == 0) ||
            (_resourceQ.Count <= _maxResourceCount))
        {
            return;
        }

        int numberToRemove = _resourceQ.Count - _maxResourceCount;

        for (int i = 0; i < numberToRemove; i++)
        {
            if (_resourceQ.TryDequeue(out string? id) &&
                (!string.IsNullOrEmpty(id)))
            {
                string[] components = id.Split('/');

                switch (components.Length)
                {
                    // resource and id
                    case 2:
                        {
                            if (_store.ContainsKey(components[0]))
                            {
                                _store[components[0]].InstanceDelete(components[1], _protectedResources);
                            }
                        }
                        break;

                    // TODO: handle versioned resources
                    // resource, id, and version
                    case 3:
                        {
                            if (_store.ContainsKey(components[0]))
                            {
                                _store[components[0]].InstanceDelete(components[1], _protectedResources);
                            }
                        }
                        break;
                }
            }
        }
    }

    /// <summary>Check received notification usage.</summary>
    private void CheckReceivedNotificationUsage()
    {
        // check received notification usage
        if (!_receivedNotifications.Any())
        {
            return;
        }

        List<string> idsToRemove = new();
        long windowTicks = DateTimeOffset.Now.Ticks - _receivedNotificationWindowTicks;

        foreach ((string id, List<ParsedSubscriptionStatus> notifications) in _receivedNotifications)
        {
            if (!notifications.Any())
            {
                idsToRemove.Add(id);
                continue;
            }

            // check oldest notification
            if (notifications.First().ProcessedDateTime.Ticks > windowTicks)
            {
                continue;
            }

            // remove all notifications that are too old
            notifications.RemoveAll(n => n.ProcessedDateTime.Ticks < windowTicks);

            if (notifications.Any())
            {
                RegisterReceivedSubscriptionChanged(id, notifications.Count, false);
            }
        }

        if (idsToRemove.Any())
        {
            foreach (string id in idsToRemove)
            {
                _ = _receivedNotifications.TryRemove(id, out _);
                RegisterReceivedSubscriptionChanged(id, 0, true);
            }
        }
    }

    /// <summary>Check and send heartbeats.</summary>
    /// <param name="state">The state.</param>
    private void CheckUsage(object? state)
    {
        CheckReceivedNotificationUsage();
        CheckResourceUsage();
    }

    /// <summary>Attempts to resolve an ITypedElement from the given string.</summary>
    /// <param name="uri">     URI of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryResolve(string uri, out ITypedElement? resource)
    {
        string[] components = uri.Split('/');

        if (components.Length < 2)
        {
            resource = null;
            return false;
        }

        string resourceType = components[components.Length - 2];
        string id = components[components.Length - 1];

        if (!_store.TryGetValue(resourceType, out IVersionedResourceStore? rs))
        {
            resource = null;
            return false;
        }

        Resource? resolved = rs.InstanceRead(id);

        if (resolved == null)
        {
            resource = null;
            return false;
        }

        resource = resolved.ToTypedElement().ToScopedNode();
        return true;
    }


    /// <summary>Attempts to resolve an ITypedElement from the given string.</summary>
    /// <param name="uri">     URI of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryResolveAsResource(string uri, out Resource? resource)
    {
        string[] components = uri.Split('/');

        if (components.Length < 2)
        {
            resource = null;
            return false;
        }

        string resourceType = components[components.Length - 2];
        string id = components[components.Length - 1];

        if (!_store.TryGetValue(resourceType, out IVersionedResourceStore? rs))
        {
            resource = null;
            return false;
        }

        resource = rs.InstanceRead(id);

        if (resource == null)
        {
            return false;
        }

        return true;
    }

    /// <summary>Resolves the given URI into a resource.</summary>
    /// <exception cref="ArgumentException">Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <param name="uri">URI of the resource.</param>
    /// <returns>An ITypedElement.</returns>
    public ITypedElement Resolve(string uri)
    {
        string[] components = uri.Split('/');

        // TODO: handle contained resources
        // TODO: handle bundle-local references

        if (components.Length < 2)
        {
            return null!;
            //throw new ArgumentException("Invalid URI", nameof(uri));
        }

        string resourceType = components[components.Length - 2];
        string id = components[components.Length - 1];

        if (!_store.TryGetValue(resourceType, out IVersionedResourceStore? rs))
        {
            return null!;
            //throw new ArgumentException("Invalid URI - unsupported resource type", nameof(uri));
        }

        Resource? resource = rs.InstanceRead(id);

        if (resource == null)
        {
            return null!;
            //throw new ArgumentException("Invalid URI - ID not found", nameof(uri));
        }

        return resource.ToTypedElement().ToScopedNode();
    }

    /// <summary>Builds outcome for request.</summary>
    /// <param name="sc">     The screen.</param>
    /// <param name="message">(Optional) The message.</param>
    /// <returns>An OperationOutcome.</returns>
    internal OperationOutcome BuildOutcomeForRequest(HttpStatusCode sc, string message = "")
    {
        if (sc.IsSuccessful())
        {
            return new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = OperationOutcome.IssueType.Unknown,
                        Diagnostics = string.IsNullOrEmpty(message)
                            ? "Request processed successfully"
                            : message,
                    },
                },
            };
        }

        return new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>()
            {
                new OperationOutcome.IssueComponent()
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.Unknown,
                    Diagnostics = string.IsNullOrEmpty(message)
                        ? $"Request failed with status code {sc.ToString()}"
                        : message,
                },
            },
        };
    }

    /// <summary>Serialize this object to the proper format.</summary>
    /// <param name="instance">   The instance.</param>
    /// <param name="destFormat"> Destination format.</param>
    /// <param name="pretty">     If the output should be 'pretty' formatted.</param>
    /// <param name="summaryType">(Optional) Type of the summary.</param>
    /// <returns>A string.</returns>
    public string SerializeFhir(
        Resource instance,
        string destFormat,
        bool pretty,
        string summaryFlag = "")
    {
        // TODO: Need to add support for count

        switch (destFormat)
        {
            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                {
                    SerializationFilter? serializationFilter;

                    switch (summaryFlag.ToLowerInvariant())
                    {
                        case "":
                        case "false":
                        default:
                            serializationFilter = null;
                            break;

                        case "true":
                            serializationFilter = SerializationFilter.ForSummary();
                            break;

                        case "text":
                            serializationFilter = SerializationFilter.ForText();
                            break;

                        case "data":
                            serializationFilter = SerializationFilter.ForData();
                            break;
                    }

                    return _xmlSerializer.SerializeToString(instance, serializationFilter);
                }

            // default to JSON
            default:
                {
                    switch (summaryFlag.ToLowerInvariant())
                    {
                        case "":
                        case "false":
                        default:
                            return _jsonSerializerFull.SerializeToString(instance);

                        case "true":
                            return _jsonSerializerSummary.SerializeToString(instance);

                        case "text":
                            return _jsonSerializerText.SerializeToString(instance);

                        case "data":
                            return _jsonSerializerData.SerializeToString(instance);
                    }
                }
                //return instance.ToJson(_jsonSerializerSettings);
        }
    }

    /// <summary>Instance create.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="ifNoneExist">       if none exist.</param>
    /// <param name="allowExistingId">   True to allow an existing id.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceCreate(
        string resourceType,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        string ifNoneExist,
        bool allowExistingId,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified,
        out string location)
    {
        object parsed;

        switch (sourceFormat)
        {
            case "json":
            case "fhir+json":
            case "application/json":
            case "application/fhir+json":
                try
                {
                    parsed = _jsonParser.DeserializeResource(content);
                }
                catch (Exception ex)
                {
                    serializedResource = string.Empty;

                    OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"JSON Parse failed: {ex.Message}");
                    serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                    eTag = string.Empty;
                    lastModified = string.Empty;
                    location = string.Empty;
                    return HttpStatusCode.BadRequest;
                }
                break;

            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                try
                {
                    parsed = _xmlParser.DeserializeResource(content);
                }
                catch (Exception ex)
                {
                    serializedResource = string.Empty;

                    OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"XML Parse failed: {ex.Message}");
                    serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                    eTag = string.Empty;
                    lastModified = string.Empty;
                    location = string.Empty;
                    return HttpStatusCode.BadRequest;
                }
                break;

            default:
                {
                    serializedResource = string.Empty;
                    
                    OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                    serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                    eTag = string.Empty;
                    lastModified = string.Empty;
                    location = string.Empty;
                    return HttpStatusCode.UnsupportedMediaType;
                }
        }

        if (parsed == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Failed to parse resource content");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        if (parsed is not Resource r)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Data is not a valid FHIR resource");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        if (string.IsNullOrEmpty(resourceType))
        {
            resourceType = r.TypeName;
        }

        if (r.TypeName != resourceType)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {r.TypeName} does not match request: {resourceType}");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        Resource? stored = _store[resourceType].InstanceCreate(r, allowExistingId);

        if (stored == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to create resource");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.InternalServerError;
        }

        if (_inLoad && _hasProtected)
        {
            _protectedResources.Add(resourceType + "/" + r.Id);
        }
        else if (_maxResourceCount != 0)
        {
            _resourceQ.Enqueue(resourceType + "/" + r.Id + "/" + stored.Meta.VersionId);
        }

        serializedResource = SerializeFhir(stored, destFormat, pretty, string.Empty);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.Created, $"Created {stored.TypeName}/{stored.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat, pretty);

        eTag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"";
        lastModified = stored.Meta?.LastUpdated == null ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        location = $"{_config.BaseUrl}/{resourceType}/{stored.Id}";
        return HttpStatusCode.Created;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceDelete(
        string resourceType,
        string id,
        string destFormat,
        bool pretty,
        string ifMatch,
        out string serializedResource,
        out string serializedOutcome)
    {
        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            serializedResource = string.Empty;
            serializedOutcome = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        // attempt delete
        Resource? deleted = _store[resourceType].InstanceDelete(id, _protectedResources);

        if (deleted == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Resource {id} not found");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        serializedResource = SerializeFhir(deleted, destFormat, pretty, string.Empty);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.Created, $"Deleted {resourceType}/{id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat, pretty);

        return HttpStatusCode.OK;
    }

    /// <summary>Instance read.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="summaryFlag">       The summary flag.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifModifiedSince">   if modified since.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceRead(
        string resourceType,
        string id,
        string destFormat,
        string summaryFlag,
        bool pretty,
        string ifMatch,
        string ifModifiedSince,
        string ifNoneMatch,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Resource type is required");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.BadRequest;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.BadRequest;
        }

        if (string.IsNullOrEmpty(id))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "ID required for instance-level read.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        Resource? stored = _store[resourceType].InstanceRead(id);

        if (stored == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Resource: {resourceType}/{id} not found");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.NotFound;
        }

        serializedResource = SerializeFhir(stored, destFormat, pretty, summaryFlag);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.OK, $"Read {stored.TypeName}/{stored.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat, pretty);

        eTag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"";
        lastModified = stored.Meta?.LastUpdated == null ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        return HttpStatusCode.OK;
    }

    /// <summary>Instance update.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
    /// <param name="allowCreate">       If the operation should allow a create as an update.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceUpdate(
        string resourceType,
        string id,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        string ifMatch,
        string ifNoneMatch,
        bool allowCreate,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified,
        out string location)
    {
        object parsed;

        switch (sourceFormat)
        {
            case "json":
            case "fhir+json":
            case "application/json":
            case "application/fhir+json":
                //parsed = _jsonParser.Parse(content);
                parsed = _jsonParser.DeserializeResource(content);
                break;

            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                //parsed = _xmlParser.Parse(content);
                parsed = _xmlParser.DeserializeResource(content);
                break;

            default:
                {
                    serializedResource = string.Empty;

                    OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                    serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                    eTag = string.Empty;
                    lastModified = string.Empty;
                    location = string.Empty;
                    return HttpStatusCode.UnsupportedMediaType;
                }
        }

        if (parsed == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Failed to parse resource content");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        if (parsed is not Resource r)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Data is not a valid FHIR resource");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        if (r.TypeName != resourceType)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {r.TypeName} does not match request: {resourceType}");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnprocessableEntity;
        }

        Resource? updated = _store[resourceType].InstanceUpdate(r, allowCreate, _protectedResources);

        if (updated == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to update resource");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.InternalServerError;
        }

        serializedResource = SerializeFhir(updated, destFormat, pretty, string.Empty);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.Created, $"Updated {updated.TypeName}/{updated.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat, pretty);

        eTag = string.IsNullOrEmpty(updated.Meta?.VersionId) ? string.Empty : $"W/\"{updated.Meta.VersionId}\"";
        lastModified = updated.Meta?.LastUpdated == null ? string.Empty : updated.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        location = $"{_config.BaseUrl}/{resourceType}/{updated.Id}";
        return HttpStatusCode.OK;
    }

    /// <summary>
    /// Attempts to add an executable search parameter to a given resource.
    /// </summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="spDefinition">The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TrySetExecutableSearchParameter(string resourceType, ModelInfo.SearchParamDefinition spDefinition)
    {
        if (!_store.ContainsKey(resourceType))
        {
            return false;
        }

        string c = resourceType + "." + spDefinition.Name;

        lock (_spLockObject)
        {
            if (_compiledSearchParameters.ContainsKey(c))
            {
                _ = _compiledSearchParameters.TryRemove(c, out _);
            }
        }

        _capabilitiesAreStale = true;
        _store[resourceType].SetExecutableSearchParameter(spDefinition);
        return true;
    }

    /// <summary>Attempts to remove an executable search parameter to a given resource.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="name">        The sp name/code/id.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryRemoveExecutableSearchParameter(string resourceType, string name)
    {
        if (!_store.ContainsKey(resourceType))
        {
            return false;
        }

        string c = resourceType + "." + name;

        lock (_spLockObject)
        {
            if (_compiledSearchParameters.ContainsKey(c))
            {
                _ = _compiledSearchParameters.TryRemove(c, out _);
            }
        }

        _capabilitiesAreStale = true;
        _store[resourceType].RemoveExecutableSearchParameter(name);
        return true;
    }

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="resource">    [out] The resource.</param>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetSearchParamDefinition(string resource, string name, out ModelInfo.SearchParamDefinition? spDefinition)
    {
        if (!_store.ContainsKey(resource))
        {
            spDefinition = null;
            return false;
        }

        if (ParsedSearchParameter._allResourceParameters.ContainsKey(name))
        {
            spDefinition = ParsedSearchParameter._allResourceParameters[name];
            return true;
        }

        return _store[resource].TryGetSearchParamDefinition(name, out spDefinition);
    }

    /// <summary>Removes the executable subscription topic described by topic.</summary>
    /// <param name="topic">The topic.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool RemoveExecutableSubscriptionTopic(ParsedSubscriptionTopic topic)
    {
        if (!_topics.ContainsKey(topic.Url))
        {
            return false;
        }

        // remove from all resources
        foreach (IVersionedResourceStore rs in _store.Values)
        {
            rs.RemoveExecutableSubscriptionTopic(topic.Url);
        }

        return true;
    }


    /// <summary>Sets an executable subscription topic.</summary>
    /// <param name="topic">The topic.</param>
    public bool SetExecutableSubscriptionTopic(ParsedSubscriptionTopic topic)
    {
        bool priorExisted = _topics.ContainsKey(topic.Url);

        // set our local reference
        if (priorExisted)
        {
            _topics[topic.Url] = topic;
        }
        else
        {
            _ = _topics.TryAdd(topic.Url, topic);
        }

        // check for no resource triggers
        if (!topic.ResourceTriggers.Any())
        {
            // remove from all resources
            foreach (IVersionedResourceStore rs in _store.Values)
            {
                rs.RemoveExecutableSubscriptionTopic(topic.Url);
            }

            // if we cannot execute, fail the update
            return false;
        }

        bool canExecute = false;

        // loop over all resources to account for a topic changing resources
        foreach ((string resourceName, IVersionedResourceStore rs) in _store)
        {
            bool executesOnResource = false;

            if (!topic.ResourceTriggers.ContainsKey(resourceName))
            {
                if (priorExisted)
                {
                    rs.RemoveExecutableSubscriptionTopic(topic.Url);
                }
                continue;
            }

            List<ExecutableSubscriptionInfo.InteractionOnlyTrigger> interactionTriggers = new();
            List<ExecutableSubscriptionInfo.CompiledFhirPathTrigger> fhirPathTriggers = new();
            List<ExecutableSubscriptionInfo.CompiledQueryTrigger> queryTriggers = new();
            ParsedResultParameters? resultParameters = null;


            string[] keys = new string[3] { resourceName, "*", "Resource" };

            foreach (string key in keys)
            {
                // TODO: Make sure to reduce full resource URI down to stub (e.g., not http://hl7.org/fhir/StructureDefinition/Patient)
                // TODO: Need to check event triggers once they are added
                if (!topic.ResourceTriggers.ContainsKey(key))
                {
                    continue;
                }

                // build our trigger definitions
                foreach (ParsedSubscriptionTopic.ResourceTrigger rt in topic.ResourceTriggers[key])
                {
                    bool onCreate = rt.OnCreate;
                    bool onUpdate = rt.OnUpdate;
                    bool onDelete = rt.OnDelete;

                    // not filled out means trigger on any
                    if ((!onCreate) && (!onUpdate) && (!onDelete))
                    {
                        onCreate = true;
                        onUpdate = true;
                        onDelete = true;
                    }

                    // prefer FHIRPath if present
                    if (!string.IsNullOrEmpty(rt.FhirPathCritiera))
                    {
                        string fpc = rt.FhirPathCritiera;

                        MatchCollection matches = _fhirpathVarMatcher().Matches(fpc);

                        // replace the variable with a resolve call
                        foreach (string matchValue in matches.Select(m => m.Value).Distinct())
                        {
                            fpc = fpc.Replace(matchValue, $"'{FhirPathVariableResolver._fhirPathPrefix}{matchValue.Substring(1)}'.resolve()");
                        }

                        fhirPathTriggers.Add(new(
                            onCreate,
                            onUpdate,
                            onDelete,
                            _compiler.Compile(fpc)));

                        canExecute = true;
                        executesOnResource = true;

                        continue;
                    }

                    // for query-based criteria
                    if ((!string.IsNullOrEmpty(rt.QueryPrevious)) || (!string.IsNullOrEmpty(rt.QueryCurrent)))
                    {
                        IEnumerable<ParsedSearchParameter> previousTest;
                        IEnumerable<ParsedSearchParameter> currentTest;

                        if (string.IsNullOrEmpty(rt.QueryPrevious))
                        {
                            previousTest = Array.Empty<ParsedSearchParameter>();
                        }
                        else
                        {
                            previousTest = ParsedSearchParameter.Parse(rt.QueryPrevious, this, rs, resourceName);
                        }

                        if (string.IsNullOrEmpty(rt.QueryCurrent))
                        {
                            currentTest = Array.Empty<ParsedSearchParameter>();
                        }
                        else
                        {
                            currentTest = ParsedSearchParameter.Parse(rt.QueryCurrent, this, rs, resourceName);
                        }

                        queryTriggers.Add(new(
                            onCreate,
                            onUpdate,
                            onDelete,
                            previousTest,
                            rt.CreateAutoFail,
                            rt.CreateAutoPass,
                            currentTest,
                            rt.DeleteAutoFail,
                            rt.DeleteAutoPass,
                            rt.RequireBothQueries));

                        canExecute = true;
                        executesOnResource = true;

                        continue;
                    }

                    // add triggers that do not have inherint filters beyond interactions
                    if (onCreate || onUpdate || onDelete)
                    {
                        interactionTriggers.Add(new(
                            onCreate,
                            onUpdate,
                            onDelete));

                        canExecute = true;
                        executesOnResource = true;

                        continue;
                    }
                }

                // build our inclusions
                if (topic.NotificationShapes.ContainsKey(key) &&
                    topic.NotificationShapes[key].Any())
                {
                    string includes = string.Empty;
                    string reverseIncludes = string.Empty;

                    // TODO: use first matching shape for now
                    ParsedSubscriptionTopic.NotificationShape shape = topic.NotificationShapes[key].First();

                    if (shape.Includes?.Any() ?? false)
                    {
                        includes = string.Join('&', shape.Includes);
                    }

                    if (shape.ReverseIncludes?.Any() ?? false)
                    {
                        reverseIncludes = string.Join('&', shape.ReverseIncludes);
                    }

                    if (string.IsNullOrEmpty(includes) && string.IsNullOrEmpty(reverseIncludes))
                    {
                        resultParameters = null;
                    }
                    else if (string.IsNullOrEmpty(includes))
                    {
                        resultParameters = new(reverseIncludes, this);
                    }
                    else if (string.IsNullOrEmpty(reverseIncludes))
                    {
                        resultParameters = new(includes, this);
                    }
                    else
                    {
                        resultParameters = new(includes + "&" + reverseIncludes, this);
                    }
                }

                // either update or remove this topic from this resource
                if (executesOnResource)
                {
                    // update the executable definition for the current resource
                    rs.SetExecutableSubscriptionTopic(
                        topic.Url,
                        interactionTriggers,
                        fhirPathTriggers,
                        queryTriggers,
                        resultParameters);
                }
                else
                {
                    rs.RemoveExecutableSubscriptionTopic(topic.Url);
                }
            }
        }

        //RegisterSubscriptionsChanged();

        return canExecute;
    }

    /// <summary>Gets subscription event count.</summary>
    /// <param name="subscriptionId">Identifier for the subscription.</param>
    /// <param name="increment">     True to increment.</param>
    /// <returns>The subscription event count.</returns>
    public long GetSubscriptionEventCount(string subscriptionId, bool increment)
    {
        if (!_subscriptions.ContainsKey(subscriptionId))
        {
            return 0;
        }

        if (increment)
        {
            return _subscriptions[subscriptionId].IncrementEventCount();
        }

        return _subscriptions[subscriptionId].CurrentEventCount;
    }

    /// <summary>Registers the subscriptions changed.</summary>
    /// <param name="subscription"> The subscription.</param>
    /// <param name="removed">      (Optional) True if removed.</param>
    /// <param name="sendHandshake">(Optional) True to send handshake.</param>
    public void RegisterSubscriptionsChanged(
        ParsedSubscription? subscription,
        bool removed = false,
        bool sendHandshake = false)
    {
        EventHandler<SubscriptionChangedEventArgs>? handler = OnSubscriptionsChanged;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                ChangedSubscription = subscription,
                RemovedSubscriptionId = removed ? subscription?.Id : null,
                SendHandshake = sendHandshake,
            });
        }
    }

    /// <summary>Registers the event.</summary>
    /// <param name="subscriptionId">   Identifier for the subscription.</param>
    /// <param name="subscriptionEvent">The subscription event.</param>
    public void RegisterSendEvent(string subscriptionId, SubscriptionEvent subscriptionEvent)
    {
        _subscriptions[subscriptionId].RegisterEvent(subscriptionEvent);

        EventHandler<SubscriptionSendEventArgs>? handler = OnSubscriptionSendEvent;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                Subscription = _subscriptions[subscriptionId],
                NotificationEvents = new List<SubscriptionEvent>() { subscriptionEvent },
                NotificationType = ParsedSubscription.NotificationTypeCodes.EventNotification,
            });
        }

        //StateHasChanged();
    }

    /// <summary>Registers the received subscription changed.</summary>
    /// <param name="subscriptionReference">  The subscription reference.</param>
    /// <param name="cachedNotificationCount">Number of cached notifications.</param>
    /// <param name="removed">                True if removed.</param>
    public void RegisterReceivedSubscriptionChanged(
        string subscriptionReference,
        int cachedNotificationCount,
        bool removed)
    {
        EventHandler<ReceivedSubscriptionChangedEventArgs>? handler = OnReceivedSubscriptionChanged;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                SubscriptionReference = subscriptionReference,
                CurrentBundleCount = cachedNotificationCount,
                Removed = removed,
            });
        }
    }

    /// <summary>Registers the received notification.</summary>
    /// <param name="bundleId">Identifier for the bundle.</param>
    /// <param name="status">  The parsed SubscriptionStatus information from the notification.</param>
    public void RegisterReceivedNotification(string bundleId, ParsedSubscriptionStatus status)
    {
        if (!_receivedNotifications.ContainsKey(status.SubscriptionReference))
        {
            _ = _receivedNotifications.TryAdd(status.SubscriptionReference, new());
        }

        _receivedNotifications[status.SubscriptionReference].Add(status);

        EventHandler<ReceivedSubscriptionEventArgs>? handler = OnReceivedSubscriptionEvent;

        if (handler != null)
        {
            handler(this, new()
            {
                Tenant = _config,
                BundleId = bundleId,
                Status = status,
            });
        }
    }


    /// <summary>
    /// Serialize one or more subscription events into the desired format and content level.
    /// </summary>
    /// <param name="subscriptionId">  The subscription id of the subscription the events belong to.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentType">     Override for the content type specified in the subscription.</param>
    /// <param name="contentLevel">    Override for the content level specified in the subscription.</param>
    /// <returns></returns>
    public string SerializeSubscriptionEvents(
        string subscriptionId,
        IEnumerable<long> eventNumbers,
        string notificationType,
        bool pretty,
        string contentType = "",
        string contentLevel = "")
    {
        if (_subscriptions.ContainsKey(subscriptionId))
        {
            Bundle? bundle = _subscriptionConverter.BundleForSubscriptionEvents(
                _subscriptions[subscriptionId],
                eventNumbers,
                notificationType,
                _config.BaseUrl,
                contentLevel);

            if (bundle == null)
            {
                return string.Empty;
            }

            string serialized = SerializeFhir(
                bundle,
                string.IsNullOrEmpty(contentType) ? _subscriptions[subscriptionId].ContentType : contentType,
                pretty,
                string.Empty);

            return serialized;
        }

        return string.Empty;
    }

    /// <summary>Bundle for subscription events.</summary>
    /// <param name="subscriptionId">  Identifier for the subscription.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="contentLevel">    (Optional) Override for the content level specified in the
    ///  subscription.</param>
    /// <returns>A Bundle?</returns>
    public Bundle? BundleForSubscriptionEvents(
        string subscriptionId,
        IEnumerable<long> eventNumbers,
        string notificationType,
        string contentLevel = "")
    {
        if (_subscriptions.ContainsKey(subscriptionId))
        {
            Bundle? bundle = _subscriptionConverter.BundleForSubscriptionEvents(
                _subscriptions[subscriptionId],
                eventNumbers,
                notificationType,
                _config.BaseUrl,
                contentLevel);

            return bundle;
        }

        return null;
    }

    /// <summary>Parse notification bundle.</summary>
    /// <param name="bundle">The bundle.</param>
    /// <returns>A ParsedSubscriptionStatus?</returns>
    public ParsedSubscriptionStatus? ParseNotificationBundle(
        Bundle bundle)
    {
        if ((!bundle.Entry.Any()) ||
            (bundle.Entry.First().Resource == null))
        {
            return null;
        }

        if (!_subscriptionConverter.TryParse(bundle.Entry.First().Resource, bundle.Id, out ParsedSubscriptionStatus status))
        {
            return null;
        }

        return status;
    }

    /// <summary>Status for subscription.</summary>
    /// <param name="subscriptionId">  Identifier for the subscription.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <returns>A Hl7.Fhir.Model.Resource?</returns>
    public Hl7.Fhir.Model.Resource? StatusForSubscription(
        string subscriptionId,
        string notificationType)
    {
        if (_subscriptions.ContainsKey(subscriptionId))
        {
            return _subscriptionConverter.StatusForSubscription(
                _subscriptions[subscriptionId],
                notificationType,
                _config.BaseUrl);
        }

        return null;
    }

    /// <summary>Removes the executable subscription described by subscription.</summary>
    /// <param name="subscription">The subscription.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool RemoveExecutableSubscription(ParsedSubscription subscription)
    {
        if (!_subscriptions.ContainsKey(subscription.Id))
        {
            return false;
        }

        // remove from all resources
        foreach (IVersionedResourceStore rs in _store.Values)
        {
            rs.RemoveExecutableSubscription(subscription.TopicUrl, subscription.Id);
        }

        _ = _subscriptions.TryRemove(subscription.Id, out _);

        RegisterSubscriptionsChanged(subscription, true);

        return true;
    }

    /// <summary>Sets executable subscription.</summary>
    /// <param name="subscription">The subscription.</param>
    public bool SetExecutableSubscription(ParsedSubscription subscription)
    {
        // check for existing record
        bool priorExisted = _subscriptions.ContainsKey(subscription.Id);
        string priorState;

        if (priorExisted)
        {
            priorState = _subscriptions[subscription.Id].CurrentStatus;
            _subscriptions[subscription.Id] = subscription;
        }
        else
        {
            priorState = "off";
            _ = _subscriptions.TryAdd(subscription.Id, subscription);
        }

        // check to see if we have this topic
        if (!_topics.ContainsKey(subscription.TopicUrl))
        {
            if (_inLoad)
            {
                if (!_loadReprocess!.ContainsKey("Subscription"))
                {
                    _loadReprocess.Add("Subscription", new());
                }

                _loadReprocess["Subscription"].Add(subscription);
            }

            return false;
        }

        ParsedSubscriptionTopic topic = _topics[subscription.TopicUrl];

        // loop over all resources to account for a topic changing resources
        foreach ((string resourceName, IVersionedResourceStore rs) in _store)
        {
            if (!topic.ResourceTriggers.ContainsKey(resourceName))
            {
                continue;
            }

            if (!subscription.Filters.ContainsKey(resourceName) &&
                !subscription.Filters.ContainsKey("*") &&
                !subscription.Filters.ContainsKey("Resource"))
            {
                // add an empty filter record so the engine knows about the subscription
                rs.SetExecutableSubscription(subscription.TopicUrl, subscription.Id, new());
                continue;
            }

            List<ParsedSearchParameter> parsedFilters = new();

            string[] keys = new string[3] { resourceName, "*", "Resource" };

            foreach (string key in keys)
            {
                if (!subscription.Filters.ContainsKey(key))
                {
                    continue;
                }

                foreach (ParsedSubscription.SubscriptionFilter filter in subscription.Filters[key])
                {
                    // TODO: check support for chained parameters in filters

                    // TODO: validate this is working for generic parameters (e.g., _id)

                    // TODO: support inline-defined parameters
                    if (!rs.TryGetSearchParamDefinition(filter.Name, out ModelInfo.SearchParamDefinition? spd) ||
                        spd == null)
                    {
                        Console.WriteLine($"Cannot apply filter with no search parameter definition {resourceName}?{filter.Name}");
                        continue;
                    }

                    SearchModifierCodes modifierCode = SearchModifierCodes.None;

                    if (!string.IsNullOrEmpty(filter.Modifier))
                    {
                        if (!Enum.TryParse(filter.Modifier, true, out modifierCode))
                        {
                            Console.WriteLine($"Ignoring unknown modifier: {resourceName}?{filter.Name}:{filter.Modifier}");
                        }
                    }

                    ParsedSearchParameter sp = new(
                        this,
                        rs,
                        key.Equals("*") ? "Resource" : key,
                        filter.Name,
                        filter.Modifier,
                        modifierCode,
                        string.IsNullOrEmpty(filter.Comparator) ? filter.Value : filter.Comparator + filter.Value,
                        spd);

                    parsedFilters.Add(sp);
                }

                rs.SetExecutableSubscription(subscription.TopicUrl, subscription.Id, parsedFilters);
            }
        }

        RegisterSubscriptionsChanged(subscription, false, priorState.Equals("off"));

        return true;
    }

    public HttpStatusCode SystemOperation(
        string operationName,
        string queryString,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome)
    {
        if (!_operations.ContainsKey(operationName))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Operation {operationName} does not have an executable implementation on this server.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        IFhirOperation op = _operations[operationName];

        if (!op.AllowSystemLevel)
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Operation {operationName} does not allow system-level execution.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        Resource? r = null;

        if (!string.IsNullOrEmpty(content))
        {
            object parsed;

            switch (sourceFormat)
            {
                case "json":
                case "fhir+json":
                case "application/json":
                case "application/fhir+json":
                    try
                    {
                        parsed = _jsonParser.DeserializeResource(content);
                    }
                    catch (Exception ex)
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"JSON Parse failed: {ex.Message}");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.BadRequest;
                    }
                    break;

                case "xml":
                case "fhir+xml":
                case "application/xml":
                case "application/fhir+xml":
                    try
                    {
                        parsed = _xmlParser.DeserializeResource(content);
                    }
                    catch (Exception ex)
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"XML Parse failed: {ex.Message}");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.BadRequest;
                    }
                    break;

                default:
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.UnsupportedMediaType;
                    }
            }

            if ((parsed != null) && (parsed is Resource))
            {
                r = (Resource)parsed;
            }
        }

        HttpStatusCode sc = op.DoOperation(
            this,
            string.Empty,
            null,
            string.Empty,
            queryString,
            r,
            out Resource? responseResource,
            out OperationOutcome? responseOutcome,
            out _);

        serializedResource = (responseResource == null) ? string.Empty : SerializeFhir(responseResource, destFormat, pretty, string.Empty);

        OperationOutcome outcome = (responseOutcome == null) ? BuildOutcomeForRequest(sc, $"System Operation {operationName} complete") : responseOutcome;
        serializedOutcome = SerializeFhir(outcome, destFormat, pretty);

        return sc;
    }

    /// <summary>Perform a FHIR Type-level operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="operationName">     Name of the operation.</param>
    /// <param name="queryString">       The query string.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode TypeOperation(
        string resourceType,
        string operationName,
        string queryString,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome)
    {
        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Resource type {resourceType} does not exist on this server.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        if (!_operations.ContainsKey(operationName))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Operation {operationName} does not have an executable implementation on this server.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        IFhirOperation op = _operations[operationName];

        if (!op.AllowResourceLevel)
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Operation {operationName} does not allow type-level execution.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        if (op.SupportedResources.Any() && (!op.SupportedResources.Contains(resourceType)))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Operation {operationName} is not allowed on {resourceType}.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        Resource? r = null;

        if (!string.IsNullOrEmpty(content))
        {
            object parsed;

            switch (sourceFormat)
            {
                case "json":
                case "fhir+json":
                case "application/json":
                case "application/fhir+json":
                    try
                    {
                        parsed = _jsonParser.DeserializeResource(content);
                    }
                    catch (Exception ex)
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"JSON Parse failed: {ex.Message}");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.BadRequest;
                    }
                    break;

                case "xml":
                case "fhir+xml":
                case "application/xml":
                case "application/fhir+xml":
                    try
                    {
                        parsed = _xmlParser.DeserializeResource(content);
                    }
                    catch (Exception ex)
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"XML Parse failed: {ex.Message}");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.BadRequest;
                    }
                    break;

                default:
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.UnsupportedMediaType;
                    }
            }

            if ((parsed != null) && (parsed is Resource))
            {
                r = (Resource)parsed;
            }
        }

        HttpStatusCode sc = op.DoOperation(
            this,
            resourceType,
            _store[resourceType],
            string.Empty,
            queryString,
            r,
            out Resource? responseResource,
            out OperationOutcome? responseOutcome,
            out _);

        serializedResource = (responseResource == null) ? string.Empty : SerializeFhir(responseResource, destFormat, pretty, string.Empty);

        OperationOutcome outcome = (responseOutcome == null) ? BuildOutcomeForRequest(sc, $"Type Operation {resourceType}/{operationName} complete") : responseOutcome;
        serializedOutcome = SerializeFhir(outcome, destFormat, pretty);

        return sc;
    }

    /// <summary>Performa FHIR Instance-level operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="operationName">     Name of the operation.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="queryString">       The query string.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceOperation(
        string resourceType,
        string operationName,
        string id,
        string queryString,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome)
    {
        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Resource type {resourceType} does not exist on this server.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        if (string.IsNullOrEmpty(id) ||
            !((IReadOnlyDictionary<string, Resource>)_store[resourceType]).ContainsKey(id))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Instance {resourceType}/{id} does not exist on this server.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        if (!_operations.ContainsKey(operationName))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.NotFound, $"Operation {operationName} does not have an executable implementation on this server.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.NotFound;
        }

        IFhirOperation op = _operations[operationName];

        if (!op.AllowInstanceLevel)
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Operation {operationName} does not allow instance-level execution.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        if (op.SupportedResources.Any() && (!op.SupportedResources.Contains(resourceType)))
        {
            serializedResource = string.Empty;
            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Operation {operationName} is not allowed on {resourceType}.");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        Resource? r = null;

        if (!string.IsNullOrEmpty(content))
        {
            object parsed;

            switch (sourceFormat)
            {
                case "json":
                case "fhir+json":
                case "application/json":
                case "application/fhir+json":
                    try
                    {
                        parsed = _jsonParser.DeserializeResource(content);
                    }
                    catch (Exception ex)
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"JSON Parse failed: {ex.Message}");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.BadRequest;
                    }
                    break;

                case "xml":
                case "fhir+xml":
                case "application/xml":
                case "application/fhir+xml":
                    try
                    {
                        parsed = _xmlParser.DeserializeResource(content);
                    }
                    catch (Exception ex)
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"XML Parse failed: {ex.Message}");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.BadRequest;
                    }
                    break;

                default:
                    {
                        serializedResource = string.Empty;

                        OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                        serializedOutcome = SerializeFhir(oo, destFormat, pretty);

                        return HttpStatusCode.UnsupportedMediaType;
                    }
            }

            if ((parsed != null) && (parsed is Resource))
            {
                r = (Resource)parsed;
            }
        }

        HttpStatusCode sc = op.DoOperation(
            this,
            resourceType,
            _store[resourceType],
            id,
            queryString,
            r,
            out Resource? responseResource,
            out OperationOutcome? responseOutcome,
            out _);

        serializedResource = (responseResource == null) ? string.Empty : SerializeFhir(responseResource, destFormat, pretty, string.Empty);

        OperationOutcome outcome = (responseOutcome == null) ? BuildOutcomeForRequest(sc, $"Type Operation {resourceType}/{operationName} complete") : responseOutcome;
        serializedOutcome = SerializeFhir(outcome, destFormat, pretty);

        return sc;
    }

    /// <summary>Type search.</summary>
    /// <param name="resourceType">     Type of the resource.</param>
    /// <param name="queryString">      The query string.</param>
    /// <param name="destFormat">       Destination format.</param>
    /// <param name="summaryFlag">      Summary-element filtering to apply.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="serializedBundle"> [out] The serialized bundle.</param>
    /// <param name="serializedOutcome">[out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode TypeSearch(
        string resourceType,
        string queryString,
        string destFormat,
        string summaryFlag,
        bool pretty,
        out string serializedBundle,
        out string serializedOutcome)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Resource type is required");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.BadRequest;
        }

        // parse search parameters
        IEnumerable<ParsedSearchParameter> parameters = ParsedSearchParameter.Parse(
            queryString,
            this,
            _store[resourceType],
            resourceType);

        // execute search
        IEnumerable<Resource>? results = _store[resourceType].TypeSearch(parameters);

        // parse search result parameters
        ParsedResultParameters resultParameters = new ParsedResultParameters(queryString, this);

        // we are done if there are no results found
        if (results == null)
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to search resource type: {resourceType}");
            serializedOutcome = SerializeFhir(oo, destFormat, pretty);

            return HttpStatusCode.UnsupportedMediaType;
        }

        string selfLink = $"{_config.BaseUrl}/{resourceType}";
        string selfSearchParams = string.Join('&', parameters.Where(p => !p.IgnoredParameter).Select(p => p.GetAppliedQueryString()));
        string selfResultParams = resultParameters.GetAppliedQueryString();

        if (!string.IsNullOrEmpty(selfSearchParams))
        {
            selfLink = selfLink + "?" + selfSearchParams;
        }

        if (!string.IsNullOrEmpty(selfResultParams))
        {
            selfLink = selfLink + (selfLink.Contains('?') ? '&' : '?') + selfResultParams;
        }

        // create our bundle for results
        Bundle bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = results.Count(),
            Link = new()
            {
                new Bundle.LinkComponent()
                {
                    Relation = "self",
                    Url = selfLink,
                }
            },
        };

        // TODO: check for a sort and apply to results

        HashSet<string> addedIds = new();

        foreach (Resource resource in results)
        {
            string relativeUrl = $"{resource.TypeName}/{resource.Id}";

            if (addedIds.Contains(relativeUrl))
            {
                // promote to match
                bundle.FindEntry(new ResourceReference(relativeUrl)).First().Search.Mode = Bundle.SearchEntryMode.Match;
            }
            else
            {
                // add the matched result to the bundle
                bundle.AddSearchEntry(resource, $"{_config.BaseUrl}/{relativeUrl}", Bundle.SearchEntryMode.Match);

                // track we have added this id
                addedIds.Add(relativeUrl);
            }

            // add any incuded resources
            AddInclusions(bundle, resource, resultParameters, addedIds);

            // check for include:iterate directives

            // add any reverse incuded resources
            AddReverseInclusions(bundle, resource, resultParameters, addedIds);
        }

        serializedBundle = SerializeFhir(bundle, destFormat, pretty, summaryFlag);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.OK, $"Search {resourceType}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat, pretty);

        return HttpStatusCode.OK;
    }
    
    private void AddIterativeInclusions()
    {

    }

    /// <summary>Enumerates resolve reverse inclusions in this collection.</summary>
    /// <param name="focus">           The focus.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process resolve reverse inclusions in this
    /// collection.
    /// </returns>
    internal IEnumerable<Resource> ResolveReverseInclusions(
        Resource focus,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds)
    {
        List<Resource> inclusions = new();

        string matchId = $"{focus.TypeName}/{focus.Id}";

        foreach ((string reverseResourceType, List<ModelInfo.SearchParamDefinition> sps) in resultParameters.ReverseInclusions)
        {
            if (!_store.ContainsKey(reverseResourceType))
            {
                continue;
            }

            foreach (ModelInfo.SearchParamDefinition sp in sps)
            {
                List<ParsedSearchParameter> parameters = new()
                {
                    new ParsedSearchParameter(
                        this,
                        _store[reverseResourceType],
                        reverseResourceType,
                        sp.Name!,
                        string.Empty,
                        SearchModifierCodes.None,
                        matchId,
                        sp),
                };

                // execute search
                IEnumerable<Resource>? results = _store[reverseResourceType].TypeSearch(parameters);

                if (results?.Any() ?? false)
                {
                    foreach (Resource revIncludeRes in results)
                    {
                        string id = $"{revIncludeRes.TypeName}/{revIncludeRes.Id}";

                        if (!addedIds.Contains(id))
                        {
                            // add the result to the list
                            inclusions.Add(revIncludeRes);

                            // track we have added this id
                            addedIds.Add(id);
                        }
                    }
                }
            }
        }

        return inclusions;
    }

    /// <summary>Adds a reverse inclusions.</summary>
    /// <param name="bundle">          The bundle.</param>
    /// <param name="resource">        [out] The resource.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    private void AddReverseInclusions(
        Bundle bundle,
        Resource resource,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds)
    {
        if (!resultParameters.ReverseInclusions.Any())
        {
            return;
        }

        IEnumerable<Resource> reverseInclusions = ResolveReverseInclusions(resource, resultParameters, addedIds);

        foreach (Resource inclusion in reverseInclusions)
        {
            // add the matched result to the bundle
            bundle.AddSearchEntry(inclusion, $"{_config.BaseUrl}/{resource.TypeName}/{resource.Id}", Bundle.SearchEntryMode.Include);
        }
    }

    /// <summary>Enumerates resolve inclusions in this collection.</summary>
    /// <param name="focus">           The focus.</param>
    /// <param name="focusTE">         The focus te.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    /// <param name="fpContext">       The context.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process resolve inclusions in this collection.
    /// </returns>
    internal IEnumerable<Resource> ResolveInclusions(
        Resource focus,
        ITypedElement focusTE,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds,
        FhirEvaluationContext? fpContext)
    {
        // check for include directives
        if (!resultParameters.Inclusions.ContainsKey(focus.TypeName))
        {
            return Array.Empty<Resource>();
        }

        if (fpContext == null)
        {
            fpContext = new FhirEvaluationContext(focusTE.ToScopedNode());
            fpContext.ElementResolver = Resolve;
        }

        List<Resource> inclusions = new();

        foreach (ModelInfo.SearchParamDefinition sp in resultParameters.Inclusions[focus.TypeName])
        {
            if (string.IsNullOrEmpty(sp.Expression))
            {
                continue;
            }

            IEnumerable<ITypedElement> extracted = GetCompiledSearchParameter(
                focus.TypeName,
                sp.Name ?? string.Empty,
                sp.Expression)
                .Invoke(focusTE, fpContext);

            if (!extracted.Any())
            {
                continue;
            }

            foreach (ITypedElement element in extracted)
            {
                switch (element.InstanceType)
                {
                    case "Reference":
                    case "ResourceReference":
                        break;
                    default:
                        // skip non references
                        Console.WriteLine($"AddInclusions <<< cannot include based on element of type {element.InstanceType}");
                        continue;
                }

                ResourceReference reference = element.ToPoco<ResourceReference>();

                if (TryResolveAsResource(reference.Reference, out Resource? resolved) &&
                    resolved != null)
                {
                    if (sp.Target?.Any() ?? false)
                    {
                        // verify this is a valid target type
                        ResourceType? rt = ModelInfo.FhirTypeNameToResourceType(resolved.TypeName);

                        if (rt == null ||
                            !sp.Target.Contains(rt.Value))
                        {
                            continue;
                        }
                    }

                    string includedId = $"{resolved.TypeName}/{resolved.Id}";
                    if (addedIds.Contains(includedId))
                    {
                        continue;
                    }

                    // add the matched result
                    inclusions.Add(resolved);

                    // track we have added this id
                    addedIds.Add(includedId);
                }
            }
        }

        return inclusions;
    }

    /// <summary>Adds the inclusions.</summary>
    /// <param name="bundle">          The bundle.</param>
    /// <param name="resource">        [out] The resource.</param>
    /// <param name="resultParameters">Options for controlling the result.</param>
    /// <param name="addedIds">        List of identifiers for the added.</param>
    private void AddInclusions(
        Bundle bundle,
        Resource resource,
        ParsedResultParameters resultParameters,
        HashSet<string> addedIds)
    {
        // check for include directives
        if (!resultParameters.Inclusions.ContainsKey(resource.TypeName))
        {
            return;
        }

        ITypedElement resourceTE = resource.ToTypedElement();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(resourceTE.ToScopedNode());
        fpContext.ElementResolver = Resolve;

        IEnumerable<Resource> inclusions = ResolveInclusions(resource, resourceTE, resultParameters, addedIds, fpContext);

        foreach (Resource inclusion in inclusions)
        {
            // add the matched result to the bundle
            bundle.AddSearchEntry(inclusion, $"{_config.BaseUrl}/{resource.TypeName}/{resource.Id}", Bundle.SearchEntryMode.Include);
        }
    }

    /// <summary>Gets the server capabilities.</summary>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>The capabilities.</returns>
    public HttpStatusCode GetMetadata(
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified)
    {
        if (_capabilitiesAreStale)
        {
            UpdateCapabilities();
        }

        // pass through to a normal instance read
        return InstanceRead(
            "CapabilityStatement",
            _capabilityStatementId,
            destFormat,
            string.Empty,
            pretty,
            string.Empty,
            string.Empty,
            string.Empty,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);
    }

    /// <summary>Common to firely version.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when one or more arguments are outside the
    ///  required range.</exception>
    /// <param name="v">The SupportedFhirVersions to process.</param>
    /// <returns>A FHIRVersion.</returns>
    private FHIRVersion CommonToFirelyVersion(TenantConfiguration.SupportedFhirVersions v)
    {
        switch (v)
        {
            case TenantConfiguration.SupportedFhirVersions.R4:
                return FHIRVersion.N4_0_1;

            case TenantConfiguration.SupportedFhirVersions.R4B:
                return FHIRVersion.N4_3_0;

            case TenantConfiguration.SupportedFhirVersions.R5:
                return FHIRVersion.N5_0_0;

            default:
                throw new ArgumentOutOfRangeException(nameof(v), $"Unsupported FHIR version: {v}");
        }
    }

    /// <summary>Updates the current capabilities of this store.</summary>
    private void UpdateCapabilities()
    {
        CapabilityStatement cs = new()
        {
            Id = _capabilityStatementId,
            Url = $"{_config.BaseUrl}/CapabilityStatement/{_capabilityStatementId}",
            Name = "Capabilities" + _config.FhirVersion,
            Status = PublicationStatus.Active,
            Date = DateTimeOffset.Now.ToFhirDateTime(),
            Kind = CapabilityStatementKind.Instance,
            Software = new()
            {
                Name = "fhir-candle",
            },
            FhirVersion = CommonToFirelyVersion(_config.FhirVersion),
            Format = _config.SupportedFormats,
            Rest = new(),
        };

        // start building our rest component
        // commented-out capabilities are ones that are not yet implemented
        CapabilityStatement.RestComponent restComponent = new()
        {
            Mode = CapabilityStatement.RestfulCapabilityMode.Server,
            Resource = new(),
            Interaction = new()
            {
                //new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.Batch },
                //new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.HistorySystem },
                //new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.SearchSystem },
                //new() { Code = Hl7.Fhir.Model.CapabilityStatement.SystemRestfulInteraction.Transaction },
            },
            //SearchParam = new(),      // currently, search parameters are expanded out to all-resource
            Operation = _operations.Values
                    .Where(o => o.AllowSystemLevel)
                    .Select(o => new CapabilityStatement.OperationComponent()
                    {
                        Name = o.OperationName,
                        Definition = o.CanonicalByFhirVersion[_config.FhirVersion],
                    }).ToList(),
            //Compartment = new(),
        };

        // add our resources
        foreach ((string resourceName, IVersionedResourceStore resourceStore) in _store)
        {
            // commented-out capabilities are ones that are not yet implemented
            CapabilityStatement.ResourceComponent rc = new()
            {
                Type = resourceName,
                Interaction = new()
                {
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Create },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Delete },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.HistoryInstance },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.HistoryType },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Patch },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Read },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType },
                    new() { Code = CapabilityStatement.TypeRestfulInteraction.Update },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Vread },
                },
                Versioning = CapabilityStatement.ResourceVersionPolicy.NoVersion,
                //ReadHistory = true,
                UpdateCreate = true,
                //ConditionalCreate = true,
                ConditionalRead = CapabilityStatement.ConditionalReadStatus.NotSupported,
                //ConditionalUpdate = true,
                //ConditionalPatch = true,
                ConditionalDelete = CapabilityStatement.ConditionalDeleteStatus.NotSupported,
                ReferencePolicy = new CapabilityStatement.ReferenceHandlingPolicy?[]
                {
                    CapabilityStatement.ReferenceHandlingPolicy.Literal,
                    //CapabilityStatement.ReferenceHandlingPolicy.Logical,
                    //CapabilityStatement.ReferenceHandlingPolicy.Resolves,
                    //CapabilityStatement.ReferenceHandlingPolicy.Enforced,
                    CapabilityStatement.ReferenceHandlingPolicy.Local,
                },
                SearchInclude = resourceStore.GetSearchIncludes(),
                SearchRevInclude = resourceStore.GetSearchRevIncludes(),
                SearchParam = resourceStore.GetSearchParamDefinitions().Select(sp => new CapabilityStatement.SearchParamComponent()
                    {
                        Name = sp.Name,
                        Definition = sp.Url,
                        Type = sp.Type,
                        Documentation = sp.Description,
                    }).ToList(),
                Operation = _operations.Values
                    .Where(o => 
                        (o.AllowInstanceLevel || o.AllowResourceLevel) && 
                        ((!o.SupportedResources.Any()) || 
                          o.SupportedResources.Contains(resourceName) || 
                          o.SupportedResources.Contains("Resource") ||
                          o.SupportedResources.Contains("DomainResource")))
                    .Select(o => new CapabilityStatement.OperationComponent()
                    {
                        Name = o.OperationName,
                        Definition = o.CanonicalByFhirVersion[_config.FhirVersion],
                    }).ToList(),
            };

            // add our resource component
            restComponent.Resource.Add(rc);
        }

        // add our rest component to the capability statement
        cs.Rest.Add(restComponent);

        // update our current capabilities
        _store["CapabilityStatement"].InstanceUpdate(cs, true, _protectedResources);
        _capabilitiesAreStale = false;
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

    /// <summary>FHIR resource store on changed.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Event information.</param>
    private void ResourceStore_OnChanged(object? sender, EventArgs e)
    {
        StateHasChanged();
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
            // dispose managed state (managed objects)
            if (disposing)
            {
                _capacityMonitor?.Dispose();

                foreach (IResourceStore rs in _store.Values)
                {
                    rs.OnChanged -= ResourceStore_OnChanged;
                }
            }

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