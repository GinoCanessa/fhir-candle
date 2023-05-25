// <copyright file="TopicConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Linq;
using FhirStore.Models;
using Hl7.Fhir.Model;

namespace FhirStore.Versioned.Shims.Subscriptions;

/// <summary>A topic format converter.</summary>
public class TopicConverter
{
    /// <summary>(Immutable) The Base URL of R5 SubscriptionTopic cross-version extensions.</summary>
    private const string _urlSt5 = "http://hl7.org/fhir/5.0/StructureDefinition/extension-SubscriptionTopic.";

    private const string _urlBackport = "http://hl7.org/fhir/uv/subscriptions-backport/StructureDefinition/";

    private void ParseExtensions(
        IEnumerable<Hl7.Fhir.Model.Extension> extensions,
        out Dictionary<string, List<Hl7.Fhir.Model.DataType>> values,
        out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nestedExts)
    {
        values = new();
        nestedExts = new();

        foreach (Hl7.Fhir.Model.Extension ext in extensions)
        {
            if (string.IsNullOrEmpty(ext.Url))
            {
                continue;
            }

            string name;

            if (ext.Url.StartsWith(_urlSt5, StringComparison.Ordinal))
            {
                name = ext.Url.Substring(72);
            }
            else if (ext.Url.StartsWith(_urlBackport, StringComparison.Ordinal))
            {
                name = ext.Url.Substring(66);
            }
            else if (ext.Url.StartsWith("http"))
            {
                continue;
            }
            else
            {
                name = ext.Url;
            }

            if (ext.Extension?.Any() ?? false)
            {
                if (!nestedExts.ContainsKey(name))
                {
                    nestedExts.Add(name, new());
                }

                nestedExts[name].Add(ext.Extension);
            }

            if (!values.ContainsKey(name))
            {
                values.Add(name, new());
            }

            values[name].Add(ext.Value);
        }
    }

    private string GetEString(Dictionary<string, List<Hl7.Fhir.Model.DataType>> extensions, string name)
    {
        if (!extensions.ContainsKey(name))
        {
            return string.Empty;
        }

        switch (extensions[name].First())
        {
            case ResourceReference valRef:
                return valRef.Reference?.ToString() ?? string.Empty;
        }

        return extensions[name].First().ToString() ?? string.Empty;
    }

    private bool GetEBool(Dictionary<string, List<Hl7.Fhir.Model.DataType>> extensions, string name)
    {
        if (!extensions.ContainsKey(name))
        {
            return false;
        }

        switch (extensions[name].First())
        {
            case FhirBoolean vb:
                return vb.Value ?? false;
        }

        if (bool.TryParse(extensions[name].First().ToString() ?? string.Empty, out bool val))
        {
            return val;
        }

        return false;
    }

    private IEnumerable<string> GetEStrings(Dictionary<string, List<Hl7.Fhir.Model.DataType>> extensions, string name)
    {
        if (!extensions.ContainsKey(name))
        {
            return Array.Empty<string>();
        }

        switch (extensions[name].First())
        {
            case ResourceReference valRef:
                return extensions[name].Select(e => (e as ResourceReference)?.Reference ?? string.Empty);
        }

        return extensions[name].Select(e => e.ToString() ?? string.Empty);
    }

    /// <summary>
    /// Attempts to parse a FHIR SubscriptionTopic.
    /// </summary>
    /// <param name="topic"> The topic.</param>
    /// <param name="common">[out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object topic, out ParsedSubscriptionTopic common)
    {
        if ((topic == null) ||
            (topic is not Hl7.Fhir.Model.Basic st) ||
            string.IsNullOrEmpty(st.Id) ||
            (st.Code == null) ||
            (st.Code.Coding == null) ||
            (!st.Code.Coding.Any(c =>
                c.Code.Equals("SubscriptionTopic", StringComparison.Ordinal) &&
                c.System.Equals("http://hl7.org/fhir/fhir-types", StringComparison.Ordinal))))
        {
            common = null!;
            return false;
        }

        ParseExtensions(
            st.Extension,
            out Dictionary<string, List<Hl7.Fhir.Model.DataType>> exts,
            out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nested);
        

        common = new()
        {
            Id = st.Id,
            Url = GetEString(exts, "url"),
        };

        if (nested.ContainsKey("resourceTrigger"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> rtExtensions in nested["resourceTrigger"])
            {
                ParseExtensions(
                    rtExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType>> rt,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> rtNested);

                string resourceType = GetEString(rt, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.ResourceTriggers.ContainsKey(resourceType))
                {
                    common.ResourceTriggers.Add(resourceType, new());
                }

                HashSet<string> interactions = new();
                foreach (string i in GetEStrings(rt, "supportedInteraction").Distinct())
                {
                    interactions.Add(i);
                }

                Dictionary<string, List<Hl7.Fhir.Model.DataType>> qc;
                if (rtNested.ContainsKey("queryCriteria"))
                {
                    ParseExtensions(rtNested["queryCriteria"].First(), out qc, out _);
                }
                else
                {
                    qc = new();
                }

                common.ResourceTriggers[resourceType].Add(new(
                    resourceType,
                    interactions.Contains("create"),
                    interactions.Contains("update"),
                    interactions.Contains("delete"),
                    GetEString(qc, "previous"),
                    GetEString(qc, "resultForCreate").Equals("test-passes", StringComparison.Ordinal) ? true : false,
                    GetEString(qc, "resultForCreate").Equals("test-fails", StringComparison.Ordinal) ? true : false,
                    GetEString(qc, "current"),
                    GetEString(qc, "resultForDelete").Equals("test-passes", StringComparison.Ordinal) ? true : false,
                    GetEString(qc, "resultForDelete").Equals("test-fails", StringComparison.Ordinal) ? true : false,
                    GetEBool(qc, "requireBoth"),
                    GetEString(rt, "fhirPathCriteria")));
            }
        }

        if (nested.ContainsKey("eventTrigger"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> etExtensions in nested["eventTrigger"])
            {
                ParseExtensions(
                    etExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType>> et,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> etNested);

                string resourceType = GetEString(et, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.EventTriggers.ContainsKey(resourceType))
                {
                    common.EventTriggers.Add(resourceType, new());
                }

                if ((et["event"].First() is CodeableConcept etConcept) &&
                    etConcept.Coding.Any())
                {
                    foreach (Hl7.Fhir.Model.Coding c in etConcept.Coding)
                    {
                        common.EventTriggers[resourceType].Add(new(
                            resourceType,
                            c.System ?? string.Empty,
                            c.Code ?? string.Empty,
                            c.Display ?? GetEString(et, "description")));
                    }
                }
            }
        }

        if (nested.ContainsKey("canFilterBy"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> cfExtensions in nested["canFilterBy"])
            {
                ParseExtensions(
                    cfExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType>> cf,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> cfNested);

                string resourceType = GetEString(cf, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.AllowedFilters.ContainsKey(resourceType))
                {
                    common.AllowedFilters.Add(resourceType, new());
                }

                // TODO: Grab the FHIRPath expression from the definition
                // TODO: Get Comparators and Modifiers
                common.AllowedFilters[resourceType].Add(new(
                    resourceType,
                    GetEString(cf, "filterParameter"),
                    GetEString(cf, "filterDefinition")));
            }
        }

        if (nested.ContainsKey("notificationShape"))
        {
            foreach (List<Hl7.Fhir.Model.Extension> nsExtensions in nested["notificationShape"])
            {
                ParseExtensions(
                    nsExtensions,
                    out Dictionary<string, List<Hl7.Fhir.Model.DataType>> ns,
                    out Dictionary<string, List<List<Hl7.Fhir.Model.Extension>>> nsNested);

                string resourceType = GetEString(ns, "resource");

                if (resourceType.Contains('/'))
                {
                    resourceType = resourceType.Substring(resourceType.LastIndexOf('/') + 1);
                }

                if (!common.NotificationShapes.ContainsKey(resourceType))
                {
                    common.NotificationShapes.Add(resourceType, new());
                }

                common.NotificationShapes[resourceType].Add(new(
                    resourceType,
                    GetEStrings(ns, "include")?.Select(i => "_include=" + i.Replace("&iterate=", "&_include:iterate=", StringComparison.Ordinal)).ToList() ?? new(),
                    GetEStrings(ns, "revInclude")?.Select(r => "_revinclude=" + r.Replace("&iterate=", "&_revinclude:iterate=", StringComparison.Ordinal)).ToList() ?? new()));
            }
        }

        return true;
    }
}