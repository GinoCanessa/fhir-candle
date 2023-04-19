// <copyright file="IVersionedStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using System.Net;

namespace FhirServerHarness.Storage;

/// <summary>Interface for versioned store.</summary>
public interface IFhirStore : IDisposable
{
    /// <summary>Initializes this service.</summary>
    /// <param name="config">The configuration.</param>
    void Init(ProviderConfiguration config);

    /// <summary>Gets the metadata for this store.</summary>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>The metadata.</returns>
    HttpStatusCode GetMetadata(
        string destFormat,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified);

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
    HttpStatusCode InstanceRead(
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
        out string lastModified);

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
    HttpStatusCode InstanceCreate(
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
        out string location);

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
    HttpStatusCode InstanceUpdate(
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
        out string location);

    /// <summary>Instance delete.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode InstanceDelete(
        string resourceType,
        string id,
        string destFormat,
        string ifMatch,
        out string serializedResource,
        out string serializedOutcome);

    /// <summary>Type search.</summary>
    /// <param name="resourceType">     Type of the resource.</param>
    /// <param name="queryString">      The query string.</param>
    /// <param name="destFormat">       Destination format.</param>
    /// <param name="serializedBundle"> [out] The serialized bundle.</param>
    /// <param name="serializedOutcome">[out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode TypeSearch(
        string resourceType,
        string queryString,
        string destFormat,
        out string serializedBundle,
        out string serializedOutcome);

    /// <summary>Resolves the given URI into a resource.</summary>
    /// <param name="uri">URI of the resource.</param>
    /// <returns>An ITypedElement.</returns>
    ITypedElement Resolve(string uri);

    /// <summary>Attempts to resolve an ITypedElement from the given string.</summary>
    /// <param name="uri">     URI of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryResolve(string uri, out ITypedElement? resource);

    /// <summary>Attempts to resolve an ITypedElement from the given string.</summary>
    /// <param name="uri">     URI of the resource.</param>
    /// <param name="resource">[out] The resource.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryResolveAsResource(string uri, out Resource? resource);

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="resource">    [out] The resource.</param>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryGetSearchParamDefinition(string resource, string name, out ModelInfo.SearchParamDefinition? spDefinition);

    /// <summary>
    /// Attempts to add an executable search parameter to a given resource.
    /// </summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="spDefinition">The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TrySetExecutableSearchParameter(string resourceType, ModelInfo.SearchParamDefinition spDefinition);

    /// <summary>Attempts to remove an executable search parameter to a given resource.</summary>
    /// <param name="resourceType">Type of the resource.</param>
    /// <param name="name">        The sp name/code/id.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    bool TryRemoveExecutableSearchParameter(string resourceType, string name);

    /// <summary>Gets the supported resources.</summary>
    public IEnumerable<string> SupportedResources { get; }
}
