// <copyright file="FhirStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Common.Models;
using FhirStore.Common.Storage;
using FhirStore.Models;
using FhirServerHarness.Search;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Support;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Linq.Expressions;
using System.Net;
using System.Xml.Linq;
using FhirStore.Versioned.Shims.Subscriptions;
using System.Text.RegularExpressions;
using Hl7.Fhir.Language.Debugging;
using static FhirServerHarness.Search.SearchDefinitions;

namespace FhirStore.Storage;

/// <summary>A FHIR store.</summary>
public class VersionedFhirStore : IFhirStore
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed;

    /// <summary>The compiler.</summary>
    private static FhirPathCompiler _compiler = null!;

    private FhirJsonParser _jsonParser = new(new ParserSettings()
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
    });

    private FhirJsonSerializationSettings _jsonSerializerSettings = new()
    {
        AppendNewLine = false,
        Pretty = false,
        IgnoreUnknownElements = true,
    };

    private FhirXmlParser _xmlParser = new(new ParserSettings()
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
    });

    private FhirXmlSerializationSettings _xmlSerializerSettings = new()
    {
        AppendNewLine = false,
        Pretty = false,
        IgnoreUnknownElements = true,
    };

    /// <summary>The store.</summary>
    private Dictionary<string, IResourceStore> _store = new();

    /// <summary>The search tester.</summary>
    private SearchTester _searchTester;

    /// <summary>Gets the supported resources.</summary>
    public IEnumerable<string> SupportedResources => _store.Keys.ToArray<string>();

    /// <summary>(Immutable) The cache of compiled search parameter extraction functions.</summary>
    private readonly Dictionary<string, CompiledExpression> _compiledSearchParameters = new();

    /// <summary>The subscription topic converter.</summary>
    private static TopicConverter _topicConverter = new();

    /// <summary>The subscription converter.</summary>
    private static SubscriptionConverter _subscriptionConverter = new();

    /// <summary>(Immutable) The topics, by id.</summary>
    private readonly Dictionary<string, ParsedSubscriptionTopic> _topics = new();

    /// <summary>(Immutable) The subscriptions, by id.</summary>
    private readonly Dictionary<string, ParsedSubscription> _subscriptions = new();

    /// <summary>(Immutable) The fhirpath variable matcher.</summary>
    private static readonly Regex _fhirpathVarMatcher = new("[%][\\w\\-]+", RegexOptions.Compiled);

    /// <summary>The configuration.</summary>
    private ProviderConfiguration _config = null!;

    /// <summary>True if capabilities are stale.</summary>
    private bool _capabilitiesAreStale = true;

    /// <summary>Base URI of this store.</summary>
    private Uri _baseUri = null!;

    /// <summary>(Immutable) Identifier for the capability statement.</summary>
    private const string _capabilityStatementId = "metadata";

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionedFhirStore"/> class.
    /// </summary>
    public VersionedFhirStore()
    {
        _searchTester = new() { FhirStore = this, };
    }

    /// <summary>Initializes this object.</summary>
    /// <param name="config">The configuration.</param>
    public void Init(ProviderConfiguration config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        if (string.IsNullOrEmpty(config.BaseUrl))
        {
            throw new ArgumentNullException(nameof(config.BaseUrl));
        }

        _config = config;
        _baseUri = new Uri(config.BaseUrl);
        if (_config.BaseUrl.EndsWith('/'))
        {
            _config.BaseUrl = _config.BaseUrl.Substring(0, _config.BaseUrl.Length - 1);
        }

        SymbolTable st = new SymbolTable().AddStandardFP().AddFhirExtensions();
        _compiler = new(st);

        Type rsType = typeof(ResourceStore<>);

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

            IResourceStore? irs = (IResourceStore?)Activator.CreateInstance(
                rsType.MakeGenericType(tArgs),
                this,
                _searchTester,
                _topicConverter,
                _subscriptionConverter);

            if (irs != null)
            {
                _store.Add(tn, irs);
            }
        }

        foreach (ModelInfo.SearchParamDefinition spDefinition in ModelInfo.SearchParameters)
        {
            if (spDefinition.Resource != null)
            {
                if (_store.TryGetValue(spDefinition.Resource, out IResourceStore? rs))
                {
                    rs.SetExecutableSearchParameter(spDefinition);
                }
            }
        }
    }

    /// <summary>Gets a compiled search parameter expression.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="name">        The sp name/code/id.</param>
    /// <param name="expression">  The FHIRPath expression.</param>
    /// <returns>The compiled.</returns>
    public CompiledExpression GetCompiledSearchParameter(string resourceType, string name, string expression)
    {
        string c = resourceType + "." + name;

        if (!_compiledSearchParameters.ContainsKey(c))
        {
            _compiledSearchParameters.Add(c, _compiler.Compile(expression));
        }

        return _compiledSearchParameters[c];
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

        if (!_store.TryGetValue(resourceType, out IResourceStore? rs))
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

        if (!_store.TryGetValue(resourceType, out IResourceStore? rs))
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

        if (!_store.TryGetValue(resourceType, out IResourceStore? rs))
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
                        Diagnostics = $"Request processed successfully",
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
                    Diagnostics = $"Request failed with status code {sc.ToString()}",
                },
            },
        };
    }

    /// <summary>Serialize this object to the proper format.</summary>
    /// <param name="instance">   The instance.</param>
    /// <param name="destFormat"> Destination format.</param>
    /// <param name="summaryType">(Optional) Type of the summary.</param>
    /// <returns>A string.</returns>
    internal string SerializeFhir(
        Resource instance,
        string destFormat,
        SummaryType summaryType = SummaryType.False)
    {
        // TODO: Need to add back in summary provider
        //if (summaryType == SummaryType.False)
        //{
            switch (destFormat)
            {
                case "xml":
                case "fhir+xml":
                case "application/xml":
                case "application/fhir+xml":
                    return instance.ToXml(_xmlSerializerSettings);

                // default to JSON
                default:
                    return instance.ToJson(_jsonSerializerSettings);
            }
        //}
    }

    /// <summary>Instance create.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
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
                parsed = _jsonParser.Parse(content);
                break;

            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                parsed = _xmlParser.Parse(content);
                break;

            default:
                {
                    serializedResource = string.Empty;
                    
                    OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                    serializedOutcome = SerializeFhir(oo, destFormat);

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
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        if (parsed is not Resource r)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Data is not a valid FHIR resource");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        if (r.TypeName != resourceType)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {r.TypeName} does not match request: {resourceType}");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        Resource? stored = _store[resourceType].InstanceCreate(r, allowExistingId);

        if (stored == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to create resource");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            location = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        serializedResource = SerializeFhir(stored, destFormat, SummaryType.False);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.Created, $"Created {stored.TypeName}/{stored.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat);

        eTag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"";
        lastModified = (stored.Meta?.LastUpdated == null) ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        location = $"{resourceType}/{stored.Id}";   // TODO: add in base url
        return HttpStatusCode.Created;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceDelete(
        string resourceType,
        string id,
        string destFormat,
        string ifMatch,
        out string serializedResource,
        out string serializedOutcome)
    {
        throw new NotImplementedException();
    }

    /// <summary>Instance read.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="summaryFlag">       The summary flag.</param>
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
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.BadRequest;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.BadRequest;
        }

        if (string.IsNullOrEmpty(id))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "ID required for instance-level read.");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        Resource? stored = _store[resourceType].InstanceRead(id);

        if (stored == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to read resource: {resourceType}/{id}");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        serializedResource = SerializeFhir(stored, destFormat, SummaryType.False);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.OK, $"Read {stored.TypeName}/{stored.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat);

        eTag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"";
        lastModified = (stored.Meta?.LastUpdated == null) ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        return HttpStatusCode.OK;
    }

    /// <summary>Instance update.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
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
        string ifMatch,
        string ifNoneMatch,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified,
        out string location)
    {
        throw new NotImplementedException();
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
        if (_compiledSearchParameters.ContainsKey(c))
        {
            _compiledSearchParameters.Remove(c);
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
        if (_compiledSearchParameters.ContainsKey(c))
        {
            _compiledSearchParameters.Remove(c);
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
            _topics.Add(topic.Url, topic);
        }

        // check for no resource triggers
        if (!topic.ResourceTriggers.Any())
        {
            // remove from all resources
            foreach (IResourceStore rs in _store.Values)
            {
                rs.RemoveExecutableSubscriptionTopic(topic.Url);
            }

            // if we cannot execute, fail the update
            return false;
        }

        bool canExecute = false;

        // loop over all resources to account for a topic changing resources
        foreach ((string resourceName, IResourceStore rs) in _store)
        {
            if (!topic.ResourceTriggers.ContainsKey(resourceName))
            {
                if (priorExisted)
                {
                    rs.RemoveExecutableSubscriptionTopic(topic.Url);
                }
                continue;
            }

            List<CompiledExpression> compiledTriggers = new();

            string[] keys = new string[3] { resourceName, "*", "Resource" };

            foreach (string key in keys)
            {
                if (!topic.ResourceTriggers.ContainsKey(key))
                {
                    continue;
                }

                foreach (ParsedSubscriptionTopic.ResourceTrigger rt in topic.ResourceTriggers[key])
                {
                    // prefer FHIRPath if present
                    if (!string.IsNullOrEmpty(rt.FhirPathCritiera))
                    {
                        string fpc = rt.FhirPathCritiera;

                        MatchCollection matches = _fhirpathVarMatcher.Matches(fpc);

                        // replace the variable with a resolve call
                        foreach (string matchValue in matches.Select(m => m.Value).Distinct())
                        {
                            fpc = fpc.Replace(matchValue, $"'{FhirPathVariableResolver._fhirPathPrefix}{matchValue.Substring(1)}'.resolve()");
                        }

                        compiledTriggers.Add(_compiler.Compile(fpc));

                        canExecute = true;
                    }

                    // TODO: add support for query-based criteria
                }

                // either update or remove this topic from this resource
                if (compiledTriggers.Any())
                {
                    // update the executable definition for the current resource
                    rs.SetExecutableSubscriptionTopic(topic.Url, compiledTriggers);
                }
                else
                {
                    rs.RemoveExecutableSubscriptionTopic(topic.Url);
                }
            }
        }

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

    /// <summary>Registers the event.</summary>
    /// <param name="subscriptionId">   Identifier for the subscription.</param>
    /// <param name="subscriptionEvent">The subscription event.</param>
    public void RegisterEvent(string subscriptionId, SubscriptionEvent subscriptionEvent)
    {
        _subscriptions[subscriptionId].RegisterEvent(subscriptionEvent);
    }

    /// <summary>Sets executable subscription.</summary>
    /// <param name="subscription">The subscription.</param>
    public bool SetExecutableSubscription(ParsedSubscription subscription)
    {
        // check for existing record
        bool priorExisted = _subscriptions.ContainsKey(subscription.Id);

        if (priorExisted)
        {
            _subscriptions[subscription.Id] = subscription;
        }
        else
        {
            _subscriptions.Add(subscription.Id, subscription);
        }

        // check to see if we have this topic
        if (!_topics.ContainsKey(subscription.TopicUrl))
        {
            return false;
        }

        ParsedSubscriptionTopic topic = _topics[subscription.TopicUrl];

        // loop over all resources to account for a topic changing resources
        foreach ((string resourceName, IResourceStore rs) in _store)
        {
            if (!topic.ResourceTriggers.ContainsKey(resourceName))
            {
                continue;
            }

            if ((!subscription.Filters.ContainsKey(resourceName)) &&
                (!subscription.Filters.ContainsKey("*")) &&
                (!subscription.Filters.ContainsKey("Resource")))
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
                    // TODO: validate this is working for generic parameters (e.g., _id)

                    // TODO: support inline-defined parameters
                    if ((!rs.TryGetSearchParamDefinition(filter.Name, out ModelInfo.SearchParamDefinition? spd)) ||
                        (spd == null))
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

        return true;
    }

    /// <summary>Type search.</summary>
    /// <param name="resourceType">     Type of the resource.</param>
    /// <param name="queryString">      The query string.</param>
    /// <param name="destFormat">       Destination format.</param>
    /// <param name="serializedBundle"> [out] The serialized bundle.</param>
    /// <param name="serializedOutcome">[out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode TypeSearch(
        string resourceType,
        string queryString,
        string destFormat,
        out string serializedBundle,
        out string serializedOutcome)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Resource type is required");
            serializedOutcome = SerializeFhir(oo, destFormat);

            return HttpStatusCode.BadRequest;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat);

            return HttpStatusCode.BadRequest;
        }

        // parse search parameters
        IEnumerable<ParsedSearchParameter> parameters = ParsedSearchParameter.Parse(
            queryString,
            this,
            _store[resourceType]);

        // execute search
        IEnumerable<Resource>? results = _store[resourceType].TypeSearch(parameters);

        // parse search result parameters
        ParsedResultParameters resultParameters = new ParsedResultParameters(queryString, this);

        // we are done if there are no results found
        if (results == null)
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to search resource type: {resourceType}");
            serializedOutcome = SerializeFhir(oo, destFormat);

            return HttpStatusCode.UnsupportedMediaType;
        }

        // create our bundle for results
        Bundle bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = results.Count(),
        };

        // TODO: check for a sort and apply to results

        HashSet<string> addedIds = new();

        foreach (Resource resource in results)
        {
            string id = $"{resource.TypeName}/{resource.Id}";

            if (addedIds.Contains(id))
            {
                // promote to match
                bundle.FindEntry(new ResourceReference(id)).First().Search.Mode = Bundle.SearchEntryMode.Match;
            }
            else
            {
                // add the matched result to the bundle
                bundle.AddSearchEntry(resource, $"{_config.BaseUrl}/{id}", Bundle.SearchEntryMode.Match);

                // track we have added this id
                addedIds.Add(id);
            }

            // add any incuded resources
            AddInclusions(bundle, resource, resultParameters, addedIds);

            // check for include:iterate directives

            // add any reverse incuded resources
            AddReverseInclusions(bundle, resource, resultParameters, addedIds);
        }

        serializedBundle = SerializeFhir(bundle, destFormat, SummaryType.False);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.OK, $"Search {resourceType}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat);

        return HttpStatusCode.OK;
    }
    
    private void AddIterativeInclusions()
    {

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

        string matchId = $"{resource.TypeName}/{resource.Id}";

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
                        sp.Name!, 
                        string.Empty, 
                        SearchDefinitions.SearchModifierCodes.None,
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
                            // add the result to the bundle
                            bundle.AddSearchEntry(resource, $"{_config.BaseUrl}/{id}", Bundle.SearchEntryMode.Include);

                            // track we have added this id
                            addedIds.Add(id);
                        }
                    }
                }
            }
        }
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

        ITypedElement r = resource.ToTypedElement();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(r.ToScopedNode());
        fpContext.ElementResolver = this.Resolve;

        foreach (ModelInfo.SearchParamDefinition sp in resultParameters.Inclusions[resource.TypeName])
        {
            if (string.IsNullOrEmpty(sp.Expression))
            {
                continue;
            }

            IEnumerable<ITypedElement> extracted = GetCompiledSearchParameter(resource.TypeName, sp.Name ?? string.Empty, sp.Expression).Invoke(r, fpContext);

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

                Hl7.Fhir.Model.ResourceReference reference = element.ToPoco<Hl7.Fhir.Model.ResourceReference>();

                if (TryResolveAsResource(reference.Reference, out Resource? resolved) &&
                    (resolved != null))
                {
                    if (sp.Target?.Any() ?? false)
                    {
                        // verify this is a valid target type
                        ResourceType? rt = ModelInfo.FhirTypeNameToResourceType(resolved.TypeName);

                        if ((rt == null) || 
                            (!sp.Target.Contains(rt.Value)))
                        {
                            continue;
                        }
                    }

                    string includedId = $"{resolved.TypeName}/{resolved.Id}";
                    if (addedIds.Contains(includedId))
                    {
                        continue;
                    }

                    // add the matched result to the bundle
                    bundle.AddSearchEntry(resolved, $"{_config.BaseUrl}/{includedId}", Bundle.SearchEntryMode.Include);

                    // track we have added this id
                    addedIds.Add(includedId);
                }
            }
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
    private FHIRVersion CommonToFirelyVersion(ProviderConfiguration.SupportedFhirVersions v)
    {
        switch (v)
        {
            case ProviderConfiguration.SupportedFhirVersions.R4:
                return FHIRVersion.N4_0_1;

            case ProviderConfiguration.SupportedFhirVersions.R4B:
                return FHIRVersion.N4_3_0;

            case ProviderConfiguration.SupportedFhirVersions.R5:
                return FHIRVersion.N5_0_0;

            default:
                throw new ArgumentOutOfRangeException(nameof(v), $"Unsupported FHIR version: {v}");
        }
    }

    /// <summary>Updates the current capabilities of this store.</summary>
    private void UpdateCapabilities()
    {
        Hl7.Fhir.Model.CapabilityStatement cs = new()
        {
            Id = _capabilityStatementId,
            Url = $"{_config.BaseUrl}/CapabilityStatement/{_capabilityStatementId}",
            Name = "Capabilities" + _config.FhirVersion,
            Status = PublicationStatus.Active,
            Date = new DateTimeOffset().ToFhirDateTime(),
            Kind = CapabilityStatementKind.Instance,
            Software = new()
            {
                Name = "FhirServerHarness",
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
            //Operation = new(),
            //Compartment = new(),
        };

        // add our resources
        foreach ((string resourceName, IResourceStore resourceStore) in _store)
        {
            // commented-out capabilities are ones that are not yet implemented
            CapabilityStatement.ResourceComponent rc = new()
            {
                Type = resourceName,
                Interaction = new()
                {
                    new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Create },
                    new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Delete },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.HistoryInstance },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.HistoryType },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Patch },
                    new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Read },
                    new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.SearchType },
                    new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Update },
                    //new() { Code = Hl7.Fhir.Model.CapabilityStatement.TypeRestfulInteraction.Vread },
                },
                Versioning = CapabilityStatement.ResourceVersionPolicy.Versioned,
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
                //Operation = new(),
            };

            // add our resource component
            restComponent.Resource.Add(rc);
        }

        // add our rest component to the capability statement
        cs.Rest.Add(restComponent);

        // update our current capabilities
        _store["CapabilityStatement"].InstanceUpdate(cs, true);
        _capabilitiesAreStale = false;
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