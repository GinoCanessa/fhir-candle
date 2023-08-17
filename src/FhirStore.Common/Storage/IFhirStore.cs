// <copyright file="IFhirStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using System.Collections.Concurrent;
using System.Net;

namespace FhirCandle.Storage;

/// <summary>Interface for versioned store.</summary>
public interface IFhirStore : IDisposable, IReadOnlyDictionary<string, IResourceStore>
{
    /// <summary>Occurs when On Changed.</summary>
    event EventHandler<EventArgs>? OnChanged;

    /// <summary>Occurs when a Subscription or SubscriptionTopic resource has changed.</summary>
    event EventHandler<SubscriptionChangedEventArgs>? OnSubscriptionsChanged;

    /// <summary>Occurs when on Subscription.</summary>
    event EventHandler<SubscriptionSendEventArgs>? OnSubscriptionSendEvent;

    /// <summary>Occurs when a received subscription has changed.</summary>
    event EventHandler<ReceivedSubscriptionChangedEventArgs>? OnReceivedSubscriptionChanged;

    /// <summary>Occurs when on Subscription.</summary>
    event EventHandler<ReceivedSubscriptionEventArgs>? OnReceivedSubscriptionEvent;

    /// <summary>State has changed.</summary>
    void StateHasChanged();

    /// <summary>Initializes this service.</summary>
    /// <param name="config">The configuration.</param>
    void Init(TenantConfiguration config);

    public TenantConfiguration Config { get; }

    /// <summary>Gets the metadata for this store.</summary>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>The metadata.</returns>
    HttpStatusCode GetMetadata(
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified);

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
    HttpStatusCode InstanceRead(
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
        out string lastModified);

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
    HttpStatusCode InstanceCreate(
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
        out string location);

    /// <summary>Instance update.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="ifMatch">           Criteria that must match to preform the update.</param>
    /// <param name="ifNoneMatch">       Criteria that must NOT match to preform the update.</param>
    /// <param name="allowCreate">       If the update should be allowed to create a new resource.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode InstanceUpdate(
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
        out string location);

    /// <summary>Instance delete.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode InstanceDelete(
        string resourceType,
        string id,
        string destFormat,
        bool pretty,
        string ifMatch,
        out string serializedResource,
        out string serializedOutcome);

    /// <summary>Process a Batch or Transaction bundle.</summary>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode ProcessBundle(
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome);

    /// <summary>Type search.</summary>
    /// <param name="resourceType">     Type of the resource.</param>
    /// <param name="queryString">      The query string.</param>
    /// <param name="destFormat">       Destination format.</param>
    /// <param name="summaryFlag">      Summary-element filtering to apply.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="serializedBundle"> [out] The serialized bundle.</param>
    /// <param name="serializedOutcome">[out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode TypeSearch(
        string resourceType,
        string queryString,
        string destFormat,
        string summaryFlag,
        bool pretty,
        out string serializedBundle,
        out string serializedOutcome);

    /// <summary>System operation.</summary>
    /// <param name="operationName">     Name of the operation.</param>
    /// <param name="queryString">       The query string.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode SystemOperation(
        string operationName,
        string queryString,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome);

    /// <summary>Type operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="operationName">     Name of the operation.</param>
    /// <param name="queryString">       The query string.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
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
        out string serializedOutcome);

    /// <summary>Instance operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="operationName">     Name of the operation.</param>
    /// <param name="instanceId">        Identifier for the instance.</param>
    /// <param name="queryString">       The query string.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="pretty">            If the output should be 'pretty' formatted.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceOperation(
        string resourceType,
        string operationName,
        string instanceId,
        string queryString,
        string content,
        string sourceFormat,
        string destFormat,
        bool pretty,
        out string serializedResource,
        out string serializedOutcome);

    /// <summary>
    /// Serialize one or more subscription events into the desired format and content level.
    /// </summary>
    /// <param name="subscriptionId">  The subscription id of the subscription the events belong to.</param>
    /// <param name="eventNumbers">    One or more event numbers to include.</param>
    /// <param name="notificationType">Type of notification (e.g., 'notification-event')</param>
    /// <param name="pretty">          If the output should be 'pretty' formatted.</param>
    /// <param name="contentType">     (Optional) Override for the content type specified in the
    ///  subscription.</param>
    /// <param name="contentLevel">    (Optional) Override for the content level specified in the
    ///  subscription.</param>
    /// <returns>A string.</returns>
    string SerializeSubscriptionEvents(
        string subscriptionId,
        IEnumerable<long> eventNumbers,
        string notificationType,
        bool pretty,
        string contentType = "",
        string contentLevel = "");

    bool TrySerializeToSubscription(
        ParsedSubscription subscriptionInfo,
        out string serialized,
        bool pretty,
        string destFormat = "");

    void ChangeSubscriptionStatus(string id, string status);

    /// <summary>Supports resource.</summary>
    /// <param name="resourceName">Name of the resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool SupportsResource(string resourceName);

    /// <summary>Attempts to get resource information.</summary>
    /// <param name="resource">    The resource.</param>
    /// <param name="resourceName">[out] Name of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetResourceInfo(object resource, out string resourceName, out string id);

    /// <summary>Gets the supported resources.</summary>
    IEnumerable<string> SupportedResources { get; }

    /// <summary>Gets the current topics.</summary>
    IEnumerable<ParsedSubscriptionTopic> CurrentTopics { get; }

    /// <summary>Gets the current subscriptions.</summary>
    IEnumerable<ParsedSubscription> CurrentSubscriptions { get; }

    /// <summary>Gets the received notifications.</summary>
    ConcurrentDictionary<string, List<ParsedSubscriptionStatus>> ReceivedNotifications { get; }

    /// <summary>Determine interaction.</summary>
    /// <param name="verb">   The HTTP verb.</param>
    /// <param name="url">    URL of the request.</param>
    /// <param name="message">[out] The message.</param>
    /// <returns>A Common.StoreInteractionCodes?</returns>
    Common.StoreInteractionCodes? DetermineInteraction(
        string verb,
        string url,
        out string message);


    ///// <summary>
    ///// Get the metadata from a remote fhir server.
    ///// </summary>
    ///// <param name="fhirServerUrl"></param>
    ///// <returns></returns>
    //HttpStatusCode GetRemoteMetadata(
    //    string fhirServerUrl);

    ///// <summary>
    ///// Attempt to retrieve the available remote subscription topics.
    ///// </summary>
    ///// <param name="fhirServerUrl"></param>
    ///// <param name="topics"></param>
    ///// <returns></returns>
    //HttpStatusCode GetRemoteSubscriptionTopics(
    //    string fhirServerUrl,
    //    out Dictionary<string, ParsedSubscriptionTopic?> topics);
}
