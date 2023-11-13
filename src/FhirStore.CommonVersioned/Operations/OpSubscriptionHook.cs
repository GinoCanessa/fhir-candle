// <copyright file="OpSubscriptionHook.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using FhirCandle.Operations;
using FhirCandle.Storage;
using FhirCandle.Extensions;
using System.Net;
using System.Reflection;

namespace FhirStore.CommonVersioned.Operations;

/// <summary>An operation subscription hook.</summary>
public class OpSubscriptionHook : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$subscription-hook";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { TenantConfiguration.SupportedFhirVersions.R4, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
        { TenantConfiguration.SupportedFhirVersions.R4B, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
        { TenantConfiguration.SupportedFhirVersions.R5, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => true;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => true;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => false;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => false;

    /// <summary>Gets a value indicating whether the accepts non FHIR.</summary>
    public bool AcceptsNonFhir => false;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage { get => string.Empty; }

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new();

    /// <summary>Executes the system $subscription-hook operation.</summary>
    /// <param name="auth">            The authentication.</param>
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
    public HttpStatusCode DoOperation(
        AuthorizationInfo? auth,
        VersionedFhirStore store,
        string resourceType,
        IVersionedResourceStore? resourceStore,
        string instanceId,
        Hl7.Fhir.Model.Resource? focusResource,
        string queryString,
        Hl7.Fhir.Model.Resource? bodyResource,
        string bodyContent,
        string contentType,
        out Hl7.Fhir.Model.Resource? responseResource,
        out Hl7.Fhir.Model.OperationOutcome? responseOutcome,
        out string contentLocation)
    {
        if ((bodyResource == null) ||
            (!(bodyResource is Hl7.Fhir.Model.Bundle bundle)) ||
            (!bundle.Entry.Any()) ||
            (bundle.Entry.First().Resource == null))
        {
            responseResource = null;
            responseOutcome = FhirCandle.Serialization.Utils.BuildOutcomeForRequest(
                HttpStatusCode.UnprocessableEntity,
                "Posted content is not a valid Subscription notification bundle");
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        if (string.IsNullOrEmpty(bundle.Id))
        {
            bundle.Id = Guid.NewGuid().ToString();
        }

        ParsedSubscriptionStatus? status = store.ParseNotificationBundle(bundle);

        if (status == null)
        {
            responseResource = null;
            responseOutcome = FhirCandle.Serialization.Utils.BuildOutcomeForRequest(
                HttpStatusCode.UnprocessableEntity,
                "Posted content is not a valid Subscription notification bundle");
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        // TODO: Clean up interfaces and types so we can avoid casts like this
        // store this bundle in our store
        Hl7.Fhir.Model.Resource? r = ((IVersionedResourceStore)((IFhirStore)store)["Bundle"]).InstanceCreate(auth, bundle, true);

        // register the notification received event
        store.RegisterReceivedNotification(r?.Id ?? bundle.Id, status);

        responseResource = null;
        responseOutcome = FhirCandle.Serialization.Utils.BuildOutcomeForRequest(HttpStatusCode.OK, "Subscription Notification Received");
        contentLocation = string.Empty;
        return HttpStatusCode.OK;
    }

    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        TenantConfiguration.SupportedFhirVersions fhirVersion)
    {
        Hl7.Fhir.Model.OperationDefinition def = new()
        {
            Id = OperationName.Substring(1) + "-" + OperationVersion.Replace('.', '-'),
            Name = OperationName,
            Url = CanonicalByFhirVersion[fhirVersion],
            Status = Hl7.Fhir.Model.PublicationStatus.Draft,
            Kind = IsNamedQuery ? Hl7.Fhir.Model.OperationDefinition.OperationKind.Query : Hl7.Fhir.Model.OperationDefinition.OperationKind.Operation,
            Code = OperationName.Substring(1),
            Resource = SupportedResources.CopyTargetsNullable(),
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
            Parameter = new(),
        };

        def.Parameter.Add(new()
        {
            Name = "resource",
            Use = Hl7.Fhir.Model.OperationParameterUse.In,
            Min = 1,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.Bundle,
        });

        def.Parameter.Add(new()
        {
            Name = "return",
            Use = Hl7.Fhir.Model.OperationParameterUse.Out,
            Min = 1,
            Max = "1",
            Type = Hl7.Fhir.Model.FHIRAllTypes.OperationOutcome,
        });

        return def;
    }
}
