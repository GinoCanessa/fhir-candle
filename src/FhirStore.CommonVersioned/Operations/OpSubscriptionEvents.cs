using System;
using System.Collections.Generic;
using System.Net;
using FhirStore.Models;
using Hl7.Fhir.Model;

namespace FhirStore.Operations;

public class OpSubscriptionEvents : IFhirOperation
{
	public OpSubscriptionEvents()
	{
	}

    public string OperationName => "$events";

    public Dictionary<FhirStore.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4B, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R5, "http://hl7.org/fhir/OperationDefinition/Subscription-events" },
    };

    public bool AllowGet => true;
    public bool AllowPost => true;
    public bool AllowSystemLevel => false;
    public bool AllowResourceLevel => true;
    public bool AllowInstanceLevel => true;

    public HashSet<string> SupportedResources => new()
    {
        "Subscription"
    };

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
        throw new NotImplementedException();

        //List<string> subscriptionIds = new();
        //List<string> statusFilters = new();

        //// check for path-component ID
        //if (!string.IsNullOrEmpty(instanceId))
        //{
        //    subscriptionIds.Add(instanceId);
        //}

        //// check for query string parameters
        //if (!string.IsNullOrEmpty(queryString))
        //{
        //    System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(queryString);
        //    foreach (string key in query)
        //    {
        //        if (string.IsNullOrWhiteSpace(key) ||
        //            string.IsNullOrWhiteSpace(query[key]))
        //        {
        //            continue;
        //        }

        //        switch (key)
        //        {
        //            case "id":
        //                subscriptionIds.AddRange(query[key]!.Split(','));
        //                break;

        //            case "status":
        //                statusFilters.AddRange(query[key]!.Split(','));
        //                break;
        //        }
        //    }
        //}

        //// check for body parameters
        //if ((bodyResource != null) &&
        //    (bodyResource is Hl7.Fhir.Model.Parameters bodyParams) &&
        //    (bodyParams.Parameter?.Any() ?? false))
        //{
        //    subscriptionIds.AddRange(bodyParams.Parameter
        //        .Where(p => p.Name.Equals("id", StringComparison.Ordinal))?
        //        .Select(p => p.Value.ToString() ?? string.Empty) ?? Array.Empty<string>());

        //    statusFilters.AddRange(bodyParams.Parameter
        //        .Where(p => p.Name.Equals("status", StringComparison.Ordinal))?
        //        .Select(p => p.Value.ToString() ?? string.Empty) ?? Array.Empty<string>());
        //}

        //Dictionary<string, Hl7.Fhir.Model.Resource> subscriptionStatuses = new();

        //if ((!subscriptionIds.Any()) && (!statusFilters.Any()))
        //{
        //    // add all
        //    foreach (string id in store._subscriptions.Keys)
        //    {
        //        Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(id, "query-status");
        //        if (s != null)
        //        {
        //            subscriptionStatuses.Add(id, s);
        //        }
        //    }
        //}
        //else if (!subscriptionIds.Any())
        //{
        //    // add by filter
        //    HashSet<string> filters = new(statusFilters.Distinct());

        //    foreach (ParsedSubscription sub in store._subscriptions.Values)
        //    {
        //        if (!filters.Contains(sub.CurrentStatus))
        //        {
        //            continue;
        //        }

        //        Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(sub.Id, "query-status");
        //        if (s != null)
        //        {
        //            subscriptionStatuses.Add(sub.Id, s);
        //        }
        //    }

        //}
        //else if (!statusFilters.Any())
        //{
        //    // add by id
        //    foreach (string id in subscriptionIds.Distinct())
        //    {
        //        Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(id, "query-status");
        //        if (s != null)
        //        {
        //            subscriptionStatuses.Add(id, s);
        //        }
        //    }
        //}
        //else
        //{
        //    // check filter and id
        //    HashSet<string> filters = new(statusFilters.Distinct());

        //    foreach (string id in subscriptionIds.Distinct())
        //    {
        //        if ((!store._subscriptions.ContainsKey(id)) ||
        //            (!filters.Contains(store._subscriptions[id].CurrentStatus)))
        //        {
        //            continue;
        //        }

        //        Hl7.Fhir.Model.Resource? s = store.StatusForSubscription(id, "query-status");
        //        if (s != null)
        //        {
        //            subscriptionStatuses.Add(id, s);
        //        }
        //    }
        //}

        //// create our response bundle
        //Bundle bundle = new()
        //{
        //    Type = Bundle.BundleType.Searchset,
        //    Timestamp = DateTimeOffset.Now,
        //    Entry = new(),
        //};

        //string prefix = store.Config.BaseUrl + "/Subscription/";

        //foreach ((string id, Hl7.Fhir.Model.Resource r) in subscriptionStatuses)
        //{
        //    bundle.AddSearchEntry(r, prefix + id, Bundle.SearchEntryMode.Match);
        //}

        //responseResource = bundle;
        //responseOutcome = null;
        //contentLocation = string.Empty;

        //return HttpStatusCode.OK;
    }
}

