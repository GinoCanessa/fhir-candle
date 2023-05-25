// <copyright file="ResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Search;
using FhirStore.Extensions;
using FhirStore.Models;
using FhirStore.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Language.Debugging;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Collections.Concurrent;
using System.Linq;
using FhirStore.Versioned.Shims.Extensions;
using FhirStore.Versioned.Shims.Subscriptions;
using System.Collections;
using System.Text.RegularExpressions;

namespace FhirStore.Storage;

/// <summary>A resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public class ResourceStore<T> : IVersionedResourceStore
    where T : Resource
{
    /// <summary>The store.</summary>
    private readonly VersionedFhirStore _store;

    /// <summary>Name of the resource.</summary>
    private string _resourceName = typeof(T).Name;

    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The resource store.</summary>
    private readonly ConcurrentDictionary<string, T> _resourceStore = new();

    /// <summary>The lock object.</summary>
    private object _lockObject = new();

    /// <summary>Occurs when On Changed.</summary>
    public event EventHandler<EventArgs>? OnChanged;

    /// <summary>The search tester.</summary>
    public required SearchTester _searchTester;

    /// <summary>The topic converter.</summary>
    public required TopicConverter _topicConverter;

    /// <summary>The subscription converter.</summary>
    public required SubscriptionConverter _subscriptionConverter;

    /// <summary>The search parameters for this resource, by Name.</summary>
    private Dictionary<string, ModelInfo.SearchParamDefinition> _searchParameters = new();

    /// <summary>The executable subscriptions, by subscription topic url.</summary>
    private Dictionary<string, ExecutableSubscriptionInfo> _executableSubscriptions = new();

    /// <summary>The supported includes.</summary>
    private string[] _supportedIncludes = Array.Empty<string>();

    /// <summary>The supported reverse includes.</summary>
    private string[] _supportedRevIncludes = Array.Empty<string>();

    /// <summary>Gets the keys.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, Resource>.Keys => _resourceStore.Keys;

    /// <summary>Gets the values.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    IEnumerable<Resource> IReadOnlyDictionary<string, Resource>.Values => _resourceStore.Values;

    /// <summary>Gets the number of. </summary>
    /// <typeparam name="string">   Type of the string.</typeparam>
    /// <typeparam name="Resource>">Type of the resource></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, Resource>>.Count => _resourceStore.Count;

    /// <summary>Indexer to get items within this collection using array index syntax.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The indexed item.</returns>
    Resource IReadOnlyDictionary<string, Resource>.this[string key] => _resourceStore[key];

    /// <summary>Query if 'key' contains key.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, Resource>.ContainsKey(string key) => _resourceStore.ContainsKey(key);

    /// <summary>Attempts to get value a Resource from the given string.</summary>
    /// <typeparam name="string">  Type of the string.</typeparam>
    /// <typeparam name="Resource">Type of the resource.</typeparam>
    /// <param name="key">  The key.</param>
    /// <param name="value">[out] The value.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, Resource>.TryGetValue(string key, out Resource value)
    {
        bool result = _resourceStore.TryGetValue(key, out T? tVal);
        value = tVal ?? null!;
        return result;
    }

    /// <summary>Gets the enumerator.</summary>
    /// <typeparam name="string">   Type of the string.</typeparam>
    /// <typeparam name="Resource>">Type of the resource></typeparam>
    /// <returns>The enumerator.</returns>
    IEnumerator<KeyValuePair<string, Resource>> IEnumerable<KeyValuePair<string, Resource>>.GetEnumerator() =>
            _resourceStore.Select(kvp => new KeyValuePair<string, Resource>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Gets the enumerator.</summary>
    /// <returns>The enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() =>
            _resourceStore.Select(kvp => new KeyValuePair<string, Resource>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>Gets the keys.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    IEnumerable<string> IReadOnlyDictionary<string, object>.Keys => _resourceStore.Keys;

    /// <summary>Gets the values.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    IEnumerable<object> IReadOnlyDictionary<string, object>.Values => _resourceStore.Values;

    /// <summary>Gets the number of. </summary>
    /// <typeparam name="string"> Type of the string.</typeparam>
    /// <typeparam name="object>">Type of the object></typeparam>
    int IReadOnlyCollection<KeyValuePair<string, object>>.Count => _resourceStore.Count;

    /// <summary>Indexer to get items within this collection using array index syntax.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>The indexed item.</returns>
    object IReadOnlyDictionary<string, object>.this[string key] => _resourceStore[key];

    /// <summary>Query if 'key' contains key.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    /// <param name="key">The key.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, object>.ContainsKey(string key) => _resourceStore.ContainsKey(key);

    /// <summary>Attempts to get value an object from the given string.</summary>
    /// <typeparam name="string">Type of the string.</typeparam>
    /// <typeparam name="object">Type of the object.</typeparam>
    /// <param name="key">  The key.</param>
    /// <param name="value">[out] The value.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool IReadOnlyDictionary<string, object>.TryGetValue(string key, out object value)
    {
        bool result = _resourceStore.TryGetValue(key, out T? tVal);
        value = tVal ?? null!;
        return result;
    }

    /// <summary>Gets the enumerator.</summary>
    /// <typeparam name="string"> Type of the string.</typeparam>
    /// <typeparam name="object>">Type of the object></typeparam>
    /// <returns>The enumerator.</returns>
    IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() =>
        _resourceStore.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value)).GetEnumerator();

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceStore{T}"/> class.
    /// </summary>
    /// <param name="fhirStore">   The FHIR store.</param>
    /// <param name="searchTester">The search tester.</param>
    public ResourceStore(
        VersionedFhirStore fhirStore,
        SearchTester searchTester,
        TopicConverter topicConverter,
        SubscriptionConverter subscriptionConverter)
    {
        _store = fhirStore;
        _searchTester = searchTester;
        _topicConverter = topicConverter;
        _subscriptionConverter = subscriptionConverter;
    }

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
    /// <param name="source">         [out] The resource.</param>
    /// <param name="allowExistingId">True to allow, false to suppress the existing identifier.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    public Resource? InstanceCreate(Resource source, bool allowExistingId)
    {
        if (source == null)
        {
            return null;
        }

        if ((!allowExistingId) || string.IsNullOrEmpty(source.Id))
        {
            source.Id = Guid.NewGuid().ToString();
        }

        lock (_lockObject)
        {
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

            if (!_resourceStore.TryAdd(source.Id, (T)source))
            {
                return null;
            }
        }

        TestCreateAgainstSubscriptions((T)source);

        switch (source.TypeName)
        {
            case "Basic":
                {
                    if ((source != null) &&
                        (source is Hl7.Fhir.Model.Basic b) &&
                        (b.Code?.Coding?.Any() ?? false) &&
                        (b.Code.Coding.Any(c =>
                            c.Code.Equals("SubscriptionTopic", StringComparison.Ordinal) &&
                            c.System.Equals("http://hl7.org/fhir/fhir-types", StringComparison.Ordinal))))
                    {
                        _ = TryProcessSubscriptionTopic(source);
                    }
                }
                break;

            case "SearchParameter":
                SetExecutableSearchParameter((SearchParameter)source);
                break;

            case "SubscriptionTopic":
                // TODO: should fail the request if this fails
                _ = TryProcessSubscriptionTopic(source);
                break;

            case "Subscription":
                // TODO: should fail the request if this fails
                _ = TryProcessSubscription(source);
                break;
        }

        return source;
    }

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">            [out] The resource.</param>
    /// <param name="allowCreate">       True to allow, false to suppress the create.</param>
    /// <param name="protectedResources">The protected resources.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    public Resource? InstanceUpdate(Resource source, bool allowCreate, HashSet<string> protectedResources)
    {
        if (string.IsNullOrEmpty(source?.Id))
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

        if (protectedResources.Any() && protectedResources.Contains(_resourceName + "/" + source.Id))
        {
            return null;
        }

        T? previous;

        lock (_lockObject)
        {
            if (!_resourceStore.ContainsKey(source.Id))
            {
                if (allowCreate)
                {
                    source.Meta.VersionId = "1";
                    previous = null;
                }
                else
                {
                    return null;
                }
            }
            else if (int.TryParse(_resourceStore[source.Id].Meta?.VersionId ?? string.Empty, out int version))
            {
                source.Meta.VersionId = (version + 1).ToString();
                previous = _resourceStore[source.Id];
            }
            else
            {
                source.Meta.VersionId = "1";
                previous = _resourceStore[source.Id];
            }

            source.Meta.LastUpdated = DateTimeOffset.UtcNow;

            _resourceStore[source.Id] = (T)source;
        }

        if (previous == null)
        {
            TestCreateAgainstSubscriptions((T)source);
        }
        else
        {
            TestUpdateAgainstSubscriptions((T)source, previous);
        }

        switch (source.TypeName)
        {
            case "SearchParameter":
                SetExecutableSearchParameter((SearchParameter)source);
                break;

            case "SubscriptionTopic":
                // TODO: should fail the request if this fails
                _ = TryProcessSubscriptionTopic(source);
                break;

            case "Subscription":
                // TODO: should fail the request if this fails
                _ = TryProcessSubscription(source);
                break;
        }

        return source;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="protectedResources">The protected resources.</param>
    /// <returns>The deleted resource or null.</returns>
    public Resource? InstanceDelete(string id, HashSet<string> protectedResources)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(id))
        {
            return null;
        }

        if (protectedResources.Any() && protectedResources.Contains(_resourceName + "/" + id))
        {
            return null;
        }

        T? previous;

        lock (_lockObject)
        {
            _ = _resourceStore.TryRemove(id, out previous);
        }

        if (previous == null)
        {
            return null;
        }

        TestDeleteAgainstSubscriptions(previous);

        switch (previous.TypeName)
        {
            case "SearchParameter":
                RemoveExecutableSearchParameter((SearchParameter)(Resource)previous);
                break;

            case "SubscriptionTopic":
                // TODO: should fail the request if this fails
                _ = TryRemoveSubscriptionTopic(previous);
                break;

            case "Subscription":
                // TODO: should fail the request if this fails
                _ = TryRemoveSubscription(previous);
                break;
        }


        return previous;
    }

    /// <summary>Process the subscription topic.</summary>
    /// <param name="st">The versioned FHIR subscription topic object.</param>
    private bool TryProcessSubscriptionTopic(object st)
    {
        if (st == null)
        {
            return false;
        }

        // get a common subscription topic for execution
        if (!_topicConverter.TryParse(st, out ParsedSubscriptionTopic topic))
        {
            return false;
        }

        // process this at the store level
        return _store.SetExecutableSubscriptionTopic(topic);
    }

    /// <summary>Attempts to remove subscription topic.</summary>
    /// <param name="st">The versioned FHIR subscription topic object.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private bool TryRemoveSubscriptionTopic(object st)
    {
        if (st == null)
        {
            return false;
        }

        // get a common subscription topic for execution
        if (!_topicConverter.TryParse(st, out ParsedSubscriptionTopic topic))
        {
            return false;
        }

        // process this at the store level
        return _store.RemoveExecutableSubscriptionTopic(topic);
    }

    /// <summary>Process the subscription described by sub.</summary>
    /// <param name="sub">The versioned FHIR subscription object.</param>
    private bool TryProcessSubscription(object sub)
    {
        if (sub == null)
        {
            return false;
        }

        // get a common subscription topic for execution
        if (!_subscriptionConverter.TryParse(sub, out ParsedSubscription subscription))
        {
            return false;
        }

        // process this at the store level
        return _store.SetExecutableSubscription(subscription);
    }

    /// <summary>Attempts to remove subscription.</summary>
    /// <param name="sub">The versioned FHIR subscription object.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private bool TryRemoveSubscription(object sub)
    {
        if (sub == null)
        {
            return false;
        }

        // get a common subscription topic for execution
        if (!_subscriptionConverter.TryParse(sub, out ParsedSubscription subscription))
        {
            return false;
        }

        // process this at the store level
        return _store.RemoveExecutableSubscription(subscription);
    }


    /// <summary>Sets executable subscription topic.</summary>
    /// <param name="url">             URL of the resource.</param>
    /// <param name="compiledTriggers">The compiled triggers.</param>
    public void SetExecutableSubscriptionTopic(
        string url,
        IEnumerable<ExecutableSubscriptionInfo.InteractionOnlyTrigger> interactionTriggers,
        IEnumerable<ExecutableSubscriptionInfo.CompiledFhirPathTrigger> fhirpathTriggers,
        IEnumerable<ExecutableSubscriptionInfo.CompiledQueryTrigger> queryTriggers,
        ParsedResultParameters? resultParameters)
    {
        if (_executableSubscriptions.ContainsKey(url))
        {
            _executableSubscriptions[url].InteractionTriggers = interactionTriggers;
            _executableSubscriptions[url].FhirPathTriggers = fhirpathTriggers;
            _executableSubscriptions[url].QueryTriggers = queryTriggers;
            _executableSubscriptions[url].AdditionalContext = resultParameters;
        }
        else
        {
            _executableSubscriptions.Add(url, new()
            {
                TopicUrl = url,
                InteractionTriggers = interactionTriggers,
                FhirPathTriggers = fhirpathTriggers,
                QueryTriggers = queryTriggers,
                AdditionalContext = resultParameters,
            });
        }
    }

    /// <summary>Sets executable subscription.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    /// <param name="id">      The subscription id.</param>
    /// <param name="filters"> The compiled filters.</param>
    public void SetExecutableSubscription(string topicUrl, string id, List<ParsedSearchParameter> filters)
    {
        if (!_executableSubscriptions.ContainsKey(topicUrl))
        {
            _executableSubscriptions.Add(topicUrl, new()
            {
                TopicUrl = topicUrl,
            });
        }

        if (_executableSubscriptions[topicUrl].FiltersBySubscription.ContainsKey(id))
        {
            _executableSubscriptions[topicUrl].FiltersBySubscription[id] = filters;
        }
        else
        {
            _executableSubscriptions[topicUrl].FiltersBySubscription.Add(id, filters);
        }
    }

    /// <summary>Removes the executable subscription described by subscriptionTopicUrl.</summary>
    /// <param name="subscriptionTopicUrl">URL of the subscription topic.</param>
    public void RemoveExecutableSubscriptionTopic(string subscriptionTopicUrl)
    {
        if (_executableSubscriptions.ContainsKey(subscriptionTopicUrl))
        {
            _executableSubscriptions.Remove(subscriptionTopicUrl);
        }
    }

    /// <summary>Removes the executable subscription.</summary>
    /// <param name="topicUrl">URL of the topic.</param>
    /// <param name="id">      The subscription id.</param>
    public void RemoveExecutableSubscription(string topicUrl, string id)
    {
        if (!_executableSubscriptions.ContainsKey(topicUrl))
        {
            return;
        }

        if (!_executableSubscriptions[topicUrl].FiltersBySubscription.ContainsKey(id))
        {
            return;
        }

        _executableSubscriptions[topicUrl].FiltersBySubscription.Remove(id);
    }


    /// <summary>Performs the subscription test action.</summary>
    /// <param name="current">  The current resource POCO</param>
    /// <param name="currentTE">The current resource ITypedElement.</param>
    /// <param name="fpContext">The FHIRPath evaluation context.</param>
    private void PerformSubscriptionTest(
        T? current,
        ITypedElement? currentTE,
        T? previous,
        ITypedElement? previousTE,
        FhirEvaluationContext fpContext,
        ExecutableSubscriptionInfo.InteractionTypes interaction)
    {
        // sanity check
        switch (interaction)
        {
            case ExecutableSubscriptionInfo.InteractionTypes.Create:
                if ((current == null) ||
                    (currentTE == null))
                {
                    return;
                }
                break;

            case ExecutableSubscriptionInfo.InteractionTypes.Update:
                if ((current == null) ||
                    (currentTE == null) ||
                    (previous == null) ||
                    (previousTE == null))
                {
                    return;
                }
                break;

            case ExecutableSubscriptionInfo.InteractionTypes.Delete:
                if ((previous == null) ||
                    (previousTE == null))
                {
                    return;
                }
                break;
        }

        HashSet<string> notifiedSubscriptions = new();
        List<string> matchedTopics = new();

        foreach ((string topicUrl, ExecutableSubscriptionInfo executable) in _executableSubscriptions)
        {
            // first, check for interaction types
            if (executable.InteractionTriggers.Any())
            {
                switch (interaction)
                {
                    case ExecutableSubscriptionInfo.InteractionTypes.Create:
                        if (executable.InteractionTriggers.Any(it => it.OnCreate == true))
                        {
                            matchedTopics.Add(topicUrl);
                            continue;
                        }
                        break;

                    case ExecutableSubscriptionInfo.InteractionTypes.Update:
                        if (executable.InteractionTriggers.Any(it => it.OnUpdate == true))
                        {
                            matchedTopics.Add(topicUrl);
                            continue;
                        }
                        break;

                    case ExecutableSubscriptionInfo.InteractionTypes.Delete:
                        if (executable.InteractionTriggers.Any(it => it.OnDelete == true))
                        {
                            matchedTopics.Add(topicUrl);
                            continue;
                        }
                        break;
                }
            }

            // second, test FhirPath
            if (executable.FhirPathTriggers.Any())
            {
                bool matched = false;

                foreach (ExecutableSubscriptionInfo.CompiledFhirPathTrigger cfp in executable.FhirPathTriggers)
                {
                    ITypedElement? result;

                    if (currentTE != null)
                    {
                        result = cfp.FhirPathTrigger.Invoke(currentTE, fpContext).First() ?? null;
                    }
                    else if (previousTE != null)
                    {
                        result = cfp.FhirPathTrigger.Invoke(previousTE, fpContext).First() ?? null;
                    }
                    else
                    {
                        continue;
                    }

                    if ((result == null) ||
                        (result.Value == null) ||
                        (!(result.Value is bool val)) ||
                        (val == false))
                    {
                        continue;
                    }

                    matched = true;
                    break;
                }

                if (matched)
                {
                    matchedTopics.Add(topicUrl);
                    continue;
                }
            }

            // finally, test query
            if (executable.QueryTriggers.Any())
            {
                bool matched = false;
                bool previousPassed = false;
                bool currentPassed = false;

                foreach (ExecutableSubscriptionInfo.CompiledQueryTrigger cq in executable.QueryTriggers)
                {
                    switch (interaction)
                    {
                        case ExecutableSubscriptionInfo.InteractionTypes.Create:
                            {
                                if (!cq.OnCreate)
                                {
                                    continue;
                                }

                                previousPassed = cq.CreateAutoPasses;
                                currentPassed = _searchTester.TestForMatch(
                                    currentTE!,
                                    cq.CurrentTest,
                                    fpContext);
                            }
                            break;

                        case ExecutableSubscriptionInfo.InteractionTypes.Update:
                            {
                                if (!cq.OnUpdate)
                                {
                                    continue;
                                }

                                previousPassed = _searchTester.TestForMatch(
                                    previousTE!,
                                    cq.PreviousTest,
                                    fpContext);
                                currentPassed = _searchTester.TestForMatch(
                                    currentTE!,
                                    cq.CurrentTest,
                                    fpContext);
                            }
                            break;

                        case ExecutableSubscriptionInfo.InteractionTypes.Delete:
                            {
                                if (!cq.OnDelete)
                                {
                                    continue;
                                }

                                previousPassed = _searchTester.TestForMatch(
                                    previousTE!,
                                    cq.PreviousTest,
                                    fpContext);
                                currentPassed = cq.DeleteAutoPasses;
                            }
                            break;
                    }

                    if ((cq.RequireBothTests && previousPassed && currentPassed) ||
                        ((!cq.RequireBothTests) && (previousPassed || currentPassed)))
                    {
                        matched = true;
                        break;
                    }
                }

                if (matched)
                {
                    matchedTopics.Add(topicUrl);
                    continue;
                }
            }
        }

        Resource focus = current ?? previous!;
        ITypedElement focusTE = currentTE ?? previousTE!;

        // traverse the list of matched topics to test against subscription filters
        foreach (string topicUrl in matchedTopics)
        {
            ParsedResultParameters? additions = _executableSubscriptions[topicUrl].AdditionalContext;

            foreach ((string subscriptionId, List<ParsedSearchParameter> filters) in _executableSubscriptions[topicUrl].FiltersBySubscription)
            {
                // don't trigger twice on multiple passing filters
                if (notifiedSubscriptions.Contains(subscriptionId))
                {
                    continue;
                }

                if ((!filters.Any()) ||
                    (_searchTester.TestForMatch(focusTE, filters, fpContext)))
                {
                    notifiedSubscriptions.Add(subscriptionId);

                    List<object> additionalContext = new();

                    if (additions != null)
                    {
                        HashSet<string> addedIds = new();
                        addedIds.Add($"{focus.TypeName}/{focus.Id}");

                        IEnumerable<Resource> inclusions = _store.ResolveInclusions(
                            focus,
                            focusTE,
                            additions,
                            addedIds,
                            fpContext);

                        if (inclusions.Any())
                        {
                            additionalContext.AddRange(inclusions);
                        }

                        IEnumerable<Resource> reverses = _store.ResolveReverseInclusions(
                            focus,
                            additions,
                            addedIds);

                        if (reverses.Any())
                        {
                            additionalContext.AddRange(reverses);
                        }
                    }    

                    SubscriptionEvent subEvent = new()
                    {
                        SubscriptionId = subscriptionId,
                        TopicUrl = topicUrl,
                        EventNumber = _store.GetSubscriptionEventCount(subscriptionId, true),
                        Focus = focus,
                        AdditionalContext = additionalContext.AsEnumerable<object>(),
                    };

                    _store.RegisterSendEvent(subscriptionId, subEvent);
                }
            }
        }
    }

    /// <summary>Tests a create interaction against all subscriptions.</summary>
    /// <param name="current">The current resource version.</param>
    private void TestCreateAgainstSubscriptions(T current)
    {
        // TODO: Change this to async

        if (!_executableSubscriptions.Any())
        {
            return;
        }

        ITypedElement currentTE = current.ToTypedElement();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(currentTE.ToScopedNode());

        FhirPathVariableResolver resolver = new FhirPathVariableResolver()
        {
            NextResolver = _store.Resolve,
            Variables = new()
            {
                { "current", currentTE },
                //{ "previous", Enumerable.Empty<ITypedElement>() },
            },
        };

        fpContext.ElementResolver = resolver.Resolve;

        PerformSubscriptionTest(
            current,
            currentTE,
            null,
            null,
            fpContext,
            ExecutableSubscriptionInfo.InteractionTypes.Create);
    }

    /// <summary>Tests an update interaction against all subscriptions.</summary>
    /// <param name="current"> The current resource version.</param>
    /// <param name="previous">The previous resource version.</param>
    private void TestUpdateAgainstSubscriptions(T current, T previous)
    {
        // TODO: Change this to async

        if (!_executableSubscriptions.Any())
        {
            return;
        }

        ITypedElement currentTE = current.ToTypedElement();
        ITypedElement previousTE = previous.ToTypedElement();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(currentTE.ToScopedNode());

        FhirPathVariableResolver resolver = new FhirPathVariableResolver()
        {
            NextResolver = _store.Resolve,
            Variables = new()
            {
                { "current", currentTE },
                { "previous", previousTE },
            },
        };

        fpContext.ElementResolver = resolver.Resolve;

        PerformSubscriptionTest(
            current,
            currentTE,
            previous,
            previousTE,
            fpContext, ExecutableSubscriptionInfo.InteractionTypes.Update);
    }

    /// <summary>Tests a delete interaction against all subscriptions.</summary>
    /// <param name="previous">The previous resource version.</param>
    private void TestDeleteAgainstSubscriptions(T previous)
    {
        // TODO: Change this to async

        if (!_executableSubscriptions.Any())
        {
            return;
        }

        ITypedElement previousTE = previous.ToTypedElement();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(previousTE.ToScopedNode());

        FhirPathVariableResolver resolver = new FhirPathVariableResolver()
        {
            NextResolver = _store.Resolve,
            Variables = new()
            {
                //{ "current", currentTE },
                { "previous", previousTE },
            },
        };

        fpContext.ElementResolver = resolver.Resolve;

        PerformSubscriptionTest(
            null,
            null,
            previous,
            previousTE,
            fpContext,
            ExecutableSubscriptionInfo.InteractionTypes.Delete);
    }

    ///// <summary>Sets executable subscription topic.</summary>
    ///// <param name="topic">The topic.</param>
    //public void SetExecutableSubscriptionTopic(ParsedSubscriptionTopic topic)
    //{
    //    if (_subscriptionTopics.ContainsKey(topic.Id))
    //    {
    //        _subscriptionTopics.Remove(topic.Id);
    //    }

    //    _subscriptionTopics.Add(topic.Id, topic);
    //}

    ///// <summary>Removes the executable subscription topic described by ID.</summary>
    ///// <param name="id">The identifier.</param>
    //public void RemoveExecutableSubscriptionTopic(string id)
    //{
    //    if (_subscriptionTopics.ContainsKey(id))
    //    {
    //        _subscriptionTopics.Remove(id);
    //    }
    //}

    /// <summary>Adds or updates an executable search parameter based on a SearchParameter resource.</summary>
    /// <param name="sp">    The sp.</param>
    /// <param name="delete">(Optional) True to delete.</param>
    private void SetExecutableSearchParameter(SearchParameter sp)
    {
        if ((sp == null) ||
            (sp.Type == null))
        {
            return;
        }

        string name = sp.Code ?? sp.Name ?? sp.Id;

        ModelInfo.SearchParamDefinition spDefinition = new()
        {
            Name = name,
            Url = sp.Url,
            Description = sp.Description,
            Expression = sp.Expression,
            Target = VersionedShims.CopyTargetsToRt(sp.Target),
            Type = (SearchParamType)sp.Type!,
        };

        if (sp.Component.Any())
        {
            spDefinition.CompositeParams = sp.Component.Select(cp => cp.Definition).ToArray();
        }

        foreach (ResourceType rt in VersionedShims.CopyTargetsToRt(sp.Base) ?? Array.Empty<ResourceType>())
        {
            spDefinition.Resource = ModelInfo.ResourceTypeToFhirTypeName(rt)!;
            _store.TrySetExecutableSearchParameter(spDefinition.Resource, spDefinition);
        }
    }

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="sp">The sp.</param>
    private void RemoveExecutableSearchParameter(SearchParameter sp)
    {
        if ((sp == null) ||
            (sp.Type == null))
        {
            return;
        }

        string name = sp.Code ?? sp.Name ?? sp.Id;

        foreach (ResourceType rt in VersionedShims.CopyTargetsToRt(sp.Base) ?? Array.Empty<ResourceType>())
        {
            _store.TryRemoveExecutableSearchParameter(ModelInfo.ResourceTypeToFhirTypeName(rt)!, name);
        }
    }

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="name">The name.</param>
    public void RemoveExecutableSearchParameter(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (_searchParameters.ContainsKey(name))
        {
            _searchParameters.Remove(name);
        }
    }

    /// <summary>Adds a search parameter definition.</summary>
    /// <param name="spDefinition">The sp definition.</param>
    public void SetExecutableSearchParameter(ModelInfo.SearchParamDefinition spDefinition)
    {
        if (string.IsNullOrEmpty(spDefinition?.Name))
        {
            return;
        }

        if (spDefinition.Resource != _resourceName)
        {
            return;
        }

        _searchParameters.Add(spDefinition.Name, spDefinition);

        // check for not having a matching search parameter resource
        if (!_store.TryResolve($"SearchParameter/{_resourceName}-{spDefinition.Name}", out ITypedElement? _))
        {
            SearchParameter sp = new()
            {
                Id = $"{_resourceName}-{spDefinition.Name}",
                Name = spDefinition.Name,
                Code = spDefinition.Name,
                Url = spDefinition.Url,
                Description = spDefinition.Description,
                Expression = spDefinition.Expression,
                Target = VersionedShims.CopyTargetsNullable(spDefinition.Target),
                Type = spDefinition.Type,
            };

            if (spDefinition.CompositeParams?.Any() ?? false)
            {
                sp.Component = new();

                foreach (string composite in spDefinition.CompositeParams)
                {
                    sp.Component.Add(new()
                    {
                        Definition = composite,
                    });
                }
            }
        }
    }

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetSearchParamDefinition(string name, out ModelInfo.SearchParamDefinition? spDefinition)
    {
        if (ParsedSearchParameter._allResourceParameters.ContainsKey(name))
        {
            spDefinition = ParsedSearchParameter._allResourceParameters[name];
            return true;
        }

        return _searchParameters.TryGetValue(name, out spDefinition);
    }

    /// <summary>Gets the search parameter definitions known by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search parameter definitions in
    /// this collection.
    /// </returns>
    public IEnumerable<ModelInfo.SearchParamDefinition> GetSearchParamDefinitions() => _searchParameters.Values;

    /// <summary>Gets the search includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search includes in this
    /// collection.
    /// </returns>
    public IEnumerable<string> GetSearchIncludes() => _supportedIncludes;

    /// <summary>Gets the search reverse includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search reverse includes in this
    /// collection.
    /// </returns>
    public IEnumerable<string> GetSearchRevIncludes() => _supportedRevIncludes;

    /// <summary>Performs a type search in this resource store.</summary>
    /// <param name="query">The query.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process type search in this collection.
    /// </returns>
    public IEnumerable<Resource> TypeSearch(IEnumerable<ParsedSearchParameter> parameters)
    {
        lock (_lockObject)
        {
            foreach (T resource in _resourceStore.Values)
            {
                ITypedElement r = resource.ToTypedElement();

                if (_searchTester.TestForMatch(r, parameters))
                {
                    yield return resource;
                }
            }
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
