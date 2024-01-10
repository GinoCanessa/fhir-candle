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

    /// <summary>Loads a package.</summary>
    /// <param name="directive">         The directive.</param>
    /// <param name="directory">         Pathname of the directory.</param>
    /// <param name="packageSupplements">The package supplements.</param>
    /// <param name="includeExamples">   True to include, false to exclude the examples.</param>
    void LoadPackage(
        string directive, 
        string directory, 
        string packageSupplements, 
        bool includeExamples);

    /// <summary>Gets a list of names of the loaded packages.</summary>
    HashSet<string> LoadedPackages { get; }

    /// <summary>Gets the loaded supplements.</summary>
    HashSet<string> LoadedSupplements { get; }

    /// <summary>Gets the configuration.</summary>
    public TenantConfiguration Config { get; }

    /// <summary>Gets the metadata for this store.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool GetMetadata(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Instance read.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool InstanceRead(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Attempts to read with minimal processing (e.g., no Hooks are called).</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <param name="resource">    [out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryInstanceRead(
        string resourceType, 
        string id, 
        out object? resource);

    /// <summary>Instance create.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool InstanceCreate(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Attempts to create an instance with minimal processing (e.g., no Hooks are called).</summary>
    /// <param name="resource">       The resource.</param>
    /// <param name="allowExistingId">True to allow an existing id.</param>
    /// <param name="resourceType">   [out] Type of the resource.</param>
    /// <param name="id">             [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryInstanceCreate(
        object resource,
        bool allowExistingId,
        out string resourceType,
        out string id);

    /// <summary>Attempts to create an instance with minimal processing (e.g., no Hooks are called).</summary>
    /// <param name="content">        The content.</param>
    /// <param name="mimeType">       Type of the mime.</param>
    /// <param name="allowExistingId">True to allow an existing id.</param>
    /// <param name="resourceType">   [out] Type of the resource.</param>
    /// <param name="id">             [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryInstanceCreate(
        string content,
        string mimeType,
        bool allowExistingId,
        out string resourceType,
        out string id);

    /// <summary>Instance update.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool InstanceUpdate(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Attempts to update an instance.</summary>
    /// <param name="resource">    The resource.</param>
    /// <param name="allowCreate"> True to allow, false to suppress the create.</param>
    /// <param name="resourceType">[out] Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryInstanceUpdate(
        object resource,
        bool allowCreate,
        out string resourceType,
        out string id);

    /// <summary>Attempts to update an instance with minimal processing (e.g., no Hooks are called).</summary>
    /// <param name="content">     The content.</param>
    /// <param name="mimeType">    Type of the mime.</param>
    /// <param name="allowCreate"> True to allow, false to suppress the create.</param>
    /// <param name="resourceType">[out] Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryInstanceUpdate(
        string content,
        string mimeType,
        bool allowCreate,
        out string resourceType,
        out string id);

    /// <summary>Instance delete.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool InstanceDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Attempts to delete an instance from the store with minimal processing (e.g., no Hooks are called).</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="id">          [out] The identifier.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryInstanceDelete(string resourceType, string id);

    /// <summary>Process a Batch or Transaction bundle.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool ProcessBundle(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>System delete.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool SystemDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Type delete (based on search).</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TypeDelete(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>System search.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool SystemSearch(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Attempts to system search an object from the given string.</summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="bundle">     [out] The bundle.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TrySystemSearch(
        string queryString,
        out object? bundle);

    /// <summary>Type search.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>A HttpStatusCode.</returns>
    bool TypeSearch(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Attempts to system search an object from the given string.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="queryString"> The query string.</param>
    /// <param name="bundle">      [out] The bundle.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryTypeSearch(
        string resourceType,
        string queryString,
        out object? bundle);

    /// <summary>System operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool SystemOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Type operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TypeOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response);

    /// <summary>Instance operation.</summary>
    /// <param name="ctx">     The request context.</param>
    /// <param name="response">[out] The response data.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool InstanceOperation(
        FhirRequestContext ctx,
        out FhirResponseContext response);

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

    /// <summary>Attempts to serialize to subscription.</summary>
    /// <param name="subscriptionInfo">Information describing the subscription.</param>
    /// <param name="serialized">      [out] The serialized.</param>
    /// <param name="pretty">          If the output should be 'pretty' formatted.</param>
    /// <param name="destFormat">      (Optional) Destination format.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TrySerializeToSubscription(
        ParsedSubscription subscriptionInfo,
        out string serialized,
        bool pretty,
        string destFormat = "");

    /// <summary>Change subscription status.</summary>
    /// <param name="id">    [out] The identifier.</param>
    /// <param name="status">The status.</param>
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
