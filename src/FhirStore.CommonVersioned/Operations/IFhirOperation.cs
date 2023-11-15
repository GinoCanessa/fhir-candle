// <copyright file="IFhirOperation.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using System.Net;

namespace FhirCandle.Operations;

/// <summary>Interface for executalbe FHIR operations.</summary>
public interface IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    string OperationName { get; }

    /// <summary>Gets the operation version.</summary>
    string OperationVersion { get; }

    /// <summary>Gets the canonical by FHIR version.</summary>
    Dictionary<FhirCandle.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion { get; }

    /// <summary>Gets a value indicating whether this object is named query.</summary>
    bool IsNamedQuery { get; }

    /// <summary>Gets a value indicating whether we allow get.</summary>
    bool AllowGet { get; }

    /// <summary>Gets a value indicating whether we allow post.</summary>
    bool AllowPost { get; }

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    bool AllowSystemLevel { get; }

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    bool AllowResourceLevel { get; }

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    bool AllowInstanceLevel { get; }

    /// <summary>Gets a value indicating whether we can accept non-FHIR formats.</summary>
    bool AcceptsNonFhir { get; }

    /// <summary>Gets a value indicating whether we can return non-FHIR formats.</summary>
    bool ReturnsNonFhir { get; }

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    string RequiresPackage { get; }

    /// <summary>Gets the supported resources.</summary>
    HashSet<string> SupportedResources { get; }

    // TODO: Consider return for non-FHIR responses to operations.

    /// <summary>Executes the operation operation.</summary>
    /// <param name="ctx">             The authentication.</param>
    /// <param name="store">           The store.</param>
    /// <param name="resourceType">    Type of the resource.</param>
    /// <param name="resourceStore">   The resource store.</param>
    /// <param name="instanceId">      Identifier for the instance.</param>
    /// <param name="focusResource">   The focus resource.</param>
    /// <param name="queryString">     The query string.</param>
    /// <param name="bodyResource">    The body resource.</param>
    /// <param name="bodyContent">     The original body content.</param>
    /// <param name="contentType">     Type of the content.</param>
    /// <param name="responseResource">[out] The response resource.</param>
    /// <param name="responseOutcome"> [out] The response outcome.</param>
    /// <param name="contentLocation"> [out] The content location.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode DoOperation(
        FhirRequestContext ctx,
        Storage.VersionedFhirStore store,
        string resourceType,
        Storage.IVersionedResourceStore? resourceStore,
        string instanceId,
        Hl7.Fhir.Model.Resource? focusResource,
        string queryString,
        Hl7.Fhir.Model.Resource? bodyResource,
        string bodyContent,
        string contentType,
        out Hl7.Fhir.Model.Resource? responseResource,
        out Hl7.Fhir.Model.OperationOutcome? responseOutcome,
        out string contentLocation);

    /// <summary>Gets an OperationDefinition resource describing this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    Hl7.Fhir.Model.OperationDefinition? GetDefinition(FhirCandle.Models.TenantConfiguration.SupportedFhirVersions fhirVersion);

}

