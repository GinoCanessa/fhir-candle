// <copyright file="OpSubscriptionStatus.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using FhirStore.Models;
using Hl7.Fhir.Model;

namespace FhirStore.Operations;

/// <summary>The FHIR Subscription status operation.</summary>
public class OpSubscriptionStatus : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$status";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirStore.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-status" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4B, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-status" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R5, "http://hl7.org/fhir/OperationDefinition/Subscription-status" },
    };

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => true;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => false;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => true;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => true;

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new()
    {
        "Subscription"
    };

    /// <summary>Executes the Subscription/$status operation.</summary>
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
        List<string> subscriptionIds = new();
        List<string> statusFilters = new();

        // check for path-component ID
        if (!string.IsNullOrEmpty(instanceId))
        {
            subscriptionIds.Add(instanceId);
        }

        // check for query string parameters
        if (!string.IsNullOrEmpty(queryString))
        {
            System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(queryString);
            foreach (string key in query)
            {
                if (string.IsNullOrWhiteSpace(key) ||
                    string.IsNullOrWhiteSpace(query[key]))
                {
                    continue;
                }

                switch (key)
                {
                    case "id":
                        subscriptionIds.AddRange(query[key]!.Split(','));
                        break;

                    case "status":
                        statusFilters.AddRange(query[key]!.Split(','));
                        break;
                }
            }
        }

        // check for body parameters
        if ((bodyResource != null) &&
            (bodyResource is Hl7.Fhir.Model.Parameters bodyParams) &&
            (bodyParams.Parameter?.Any() ?? false))
        {
            subscriptionIds.AddRange(bodyParams.Parameter
                .Where(p => p.Name.Equals("id", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty) ?? Array.Empty<string>());

            statusFilters.AddRange(bodyParams.Parameter
                .Where(p => p.Name.Equals("status", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty) ?? Array.Empty<string>());
        }

        Dictionary<string, Hl7.Fhir.Model.Resource> subscriptionStatuses = new();

        if ((!subscriptionIds.Any()) && (!statusFilters.Any()))
        {
            // add all
            foreach (string id in store._subscriptions.Keys)
            {
                Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(id, "query-status");
                if (s != null)
                {
                    subscriptionStatuses.Add(id, s);
                }
            }
        }
        else if (!subscriptionIds.Any())
        {
            // add by filter
            HashSet<string> filters = new(statusFilters.Distinct());

            foreach (ParsedSubscription sub in store._subscriptions.Values)
            {
                if (!filters.Contains(sub.CurrentStatus))
                {
                    continue;
                }

                Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(sub.Id, "query-status");
                if (s != null)
                {
                    subscriptionStatuses.Add(sub.Id, s);
                }
            }

        }
        else if (!statusFilters.Any())
        {
            // add by id
            foreach (string id in subscriptionIds.Distinct())
            {
                Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(id, "query-status");
                if (s != null)
                {
                    subscriptionStatuses.Add(id, s);
                }
            }
        }
        else
        {
            // check filter and id
            HashSet<string> filters = new(statusFilters.Distinct());

            foreach (string id in subscriptionIds.Distinct())
            {
                if ((!store._subscriptions.ContainsKey(id)) ||
                    (!filters.Contains(store._subscriptions[id].CurrentStatus)))
                {
                    continue;
                }

                Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(id, "query-status");
                if (s != null)
                {
                    subscriptionStatuses.Add(id, s);
                }
            }
        }

        // create our response bundle
        Bundle bundle = new()
        {
            Type = Bundle.BundleType.Searchset,
            Timestamp = DateTimeOffset.Now,
            Entry = new(),
        };

        string prefix = store.Config.BaseUrl + "/Subscription/";

        foreach ((string id, Hl7.Fhir.Model.Resource r) in subscriptionStatuses)
        {
            bundle.AddSearchEntry(r, prefix + id, Bundle.SearchEntryMode.Match);
        }

        responseResource = bundle;
        responseOutcome = null;
        contentLocation = string.Empty;

        return HttpStatusCode.OK;
    }
}

