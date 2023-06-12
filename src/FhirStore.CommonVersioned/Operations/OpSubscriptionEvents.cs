// <copyright file="OpSubscriptionEvents.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;

namespace FhirStore.Operations;

/// <summary>The FHIR Subscription $events operation.</summary>
public class OpSubscriptionEvents : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$events";

    public string OperationVersion => "0.0.1";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirStore.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R4B, "http://hl7.org/fhir/uv/subscriptions-backport/OperationDefinition/backport-subscription-events" },
        { FhirStore.Models.TenantConfiguration.SupportedFhirVersions.R5, "http://hl7.org/fhir/OperationDefinition/Subscription-events" },
    };

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => true;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => false;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => false;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => true;

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new()
    {
        "Subscription"
    };

    /// <summary>Executes the Subscription/$events operation.</summary>
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
        string eventsSince = string.Empty;
        string eventsUntil = string.Empty;
        string contentLevel = string.Empty;

        // check for a subscription ID
        if ((string.IsNullOrEmpty(instanceId)) ||
            (!store._subscriptions.ContainsKey(instanceId)))
        {
            responseResource = null;
            responseOutcome = null;
            contentLocation = string.Empty;

            return HttpStatusCode.BadRequest;
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
                    case "events-since-number":
                    case "eventssincenumber":
                    case "eventsSinceNumber":
                        eventsSince = query[key] ?? string.Empty;
                        break;

                    case "events-until-number":
                    case "eventsuntilnumber":
                    case "eventsUntilNumber":
                        eventsUntil = query[key] ?? string.Empty;
                        break;

                    case "content":
                        contentLevel = query[key] ?? string.Empty;
                        break;
                }
            }
        }

        // check for body parameters
        if ((bodyResource != null) &&
            (bodyResource is Hl7.Fhir.Model.Parameters bodyParams) &&
            (bodyParams.Parameter?.Any() ?? false))
        {
            eventsSince = bodyParams.Parameter
                .Where(p => p.Name.Equals("eventsSinceNumber", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty)
                .First() ?? string.Empty;

            eventsUntil = bodyParams.Parameter
                .Where(p => p.Name.Equals("eventsUntilNumber", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty)
                .First() ?? string.Empty;

            contentLevel = bodyParams.Parameter
                .Where(p => p.Name.Equals("content", StringComparison.Ordinal))?
                .Select(p => p.Value.ToString() ?? string.Empty)
                .First() ?? string.Empty;
        }

        long highestEvent = store._subscriptions[instanceId].CurrentEventCount;

        if (!long.TryParse(eventsSince, out long sinceNumber))
        {
            sinceNumber = 0;
        }

        if (!long.TryParse(eventsUntil, out long untilNumber))
        {
            untilNumber = highestEvent;
        }

        List<long> eventNumbers = new();
        for (long i = sinceNumber; i <= untilNumber; i++)
        {
            eventNumbers.Add(i);
        }

        responseResource = store.BundleForSubscriptionEvents(
            instanceId,
            eventNumbers,
            "query-event",
            contentLevel);

        responseOutcome = null;
        contentLocation = string.Empty;

        return HttpStatusCode.OK;
    }
}

