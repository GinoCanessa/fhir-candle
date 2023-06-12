// <copyright file="OpSubscriptionHook.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Models;
using FhirStore.Operations;
using FhirStore.Storage;
using System.Net;

namespace FhirStore.CommonVersioned.Operations;

/// <summary>An operation subscription hook.</summary>
public class OpSubscriptionHook : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$subscription-hook";

    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirStore.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4B, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R5, "http://argo.run/fhir/OperationDefinition/subscription-hook" },
    };

    public bool IsQuery => false;

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

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new();

    /// <summary>Executes the system $subscription-hook operation.</summary>
    /// <param name="store">           The store.</param>
    /// <param name="resourceType">    Type of the resource.</param>
    /// <param name="resourceStore">   The resource store.</param>
    /// <param name="instanceId">      Identifier for the instance.</param>
    /// <param name="queryString">     The query string.</param>
    /// <param name="bodyResource">    The body resource.</param>
    /// <param name="responseResource">[out] The response resource.</param>
    /// <param name="responseOutcome"> [out] The response outcome.</param>
    /// <param name="contentLocation"> [out] The content location.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode DoOperation(
        Storage.VersionedFhirStore store,
        string resourceType,
        Storage.IVersionedResourceStore? resourceStore,
        string instanceId,
        string queryString,
        Hl7.Fhir.Model.Resource? bodyResource,
        out Hl7.Fhir.Model.Resource? responseResource,
        out Hl7.Fhir.Model.OperationOutcome? responseOutcome,
        out string contentLocation)
    {
        if ((store == null) ||
            (bodyResource == null) ||
            (!(bodyResource is Hl7.Fhir.Model.Bundle bundle)) ||
            (!bundle.Entry.Any()) ||
            (bundle.Entry.First().Resource == null))
        {
            responseResource = null;
            responseOutcome = null;
            contentLocation = string.Empty;

            return HttpStatusCode.BadRequest;
        }

        if (string.IsNullOrEmpty(bundle.Id))
        {
            bundle.Id = Guid.NewGuid().ToString();
        }

        ParsedSubscriptionStatus? status = store.ParseNotificationBundle(bundle);

        if (status == null)
        {
            responseResource = null;
            responseOutcome = null;
            contentLocation = string.Empty;

            return HttpStatusCode.BadRequest;
        }

        // TODO: Clean up interfaces and types so we can avoid casts like this
        // store this bundle in our store
        Hl7.Fhir.Model.Resource? r = ((IVersionedResourceStore)((IFhirStore)store)["Bundle"]).InstanceCreate(bundle, true);

        // register the notification received event
        store.RegisterReceivedNotification(r?.Id ?? bundle.Id, status);

        responseResource = null;
        responseOutcome = store.BuildOutcomeForRequest(HttpStatusCode.OK, "Subscription Notification Received");
        contentLocation = string.Empty;
        return HttpStatusCode.OK;
    }

    public Hl7.Fhir.Model.OperationDefinition GetDefinition(FhirStore.Models.TenantConfiguration.SupportedFhirVersions fhirVersion)
    {
        List<Hl7.Fhir.Model.ResourceType> resourceTypes = new();

        foreach (string r in SupportedResources)
        {
            try
            {
                Hl7.Fhir.Model.ResourceType? rt = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<Hl7.Fhir.Model.ResourceType>(r);
                if (rt != null)
                {
                    resourceTypes.Add((Hl7.Fhir.Model.ResourceType)rt!);
                }
            }
            catch(Exception)
            {
            }
        }

        Hl7.Fhir.Model.OperationDefinition def = new()
        {
            Id = OperationName.Substring(1) + "-" + OperationVersion.Replace('.', '-'),
            Name = OperationName,
            Url = CanonicalByFhirVersion[fhirVersion],
            Status = Hl7.Fhir.Model.PublicationStatus.Draft,
            Kind = IsQuery ? Hl7.Fhir.Model.OperationDefinition.OperationKind.Query : Hl7.Fhir.Model.OperationDefinition.OperationKind.Operation,
            Code = OperationName.Substring(1),
            Resource = (IEnumerable<Hl7.Fhir.Model.ResourceType?>)resourceTypes,
            System = AllowSystemLevel,
            Type = AllowResourceLevel,
            Instance = AllowInstanceLevel,
        };


    }
}
