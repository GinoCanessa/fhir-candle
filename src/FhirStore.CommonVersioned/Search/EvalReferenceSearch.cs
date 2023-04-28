// <copyright file="EvalReferenceSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Models;
using Hl7.Fhir.ElementModel;

namespace FhirServerHarness.Search;

/// <summary>A class that contains functions to test reference inputs against various FHIR types.</summary>
public static class EvalReferenceSearch
{
    /// <summary>Compare references common.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsCommon(Hl7.Fhir.Model.ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (s.Url.Equals(r.Reference, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.IsNullOrEmpty(s.ResourceType) &&
            (!string.IsNullOrEmpty(s.Id)) &&
            r.Reference.EndsWith("/" + s.Id, StringComparison.Ordinal))
        {
            // TODO: check resource versions

            return true;
        }

        return false;
    }

    /// <summary>Compare OID reference.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsOid(Hl7.Fhir.Model.ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (s.Url.Equals(r.Reference, StringComparison.OrdinalIgnoreCase) ||
            s.Url.Equals("urn:oid:" + r.Reference, StringComparison.OrdinalIgnoreCase) ||
            ("urn:oid:" + s.Url).Equals(r.Reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>Compare UUID reference.</summary>
    /// <param name="r">A ResourceReference to process.</param>
    /// <param name="s">A SegmentedReference to process.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool CompareRefsUuid(Hl7.Fhir.Model.ResourceReference r, ParsedSearchParameter.SegmentedReference s)
    {
        if (s.Url.Equals(r.Reference, StringComparison.OrdinalIgnoreCase) ||
            s.Url.Equals("urn:uuid:" + r.Reference, StringComparison.OrdinalIgnoreCase) ||
            ("urn:uuid:" + s.Url).Equals(r.Reference, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    /// <summary>Tests reference against most FHIR types.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReference(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (valueNode.InstanceType != "Reference") ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        if (string.IsNullOrEmpty(r.Reference))
        {
            return false;
        }

        return sp.ValueReferences.Any(s => CompareRefsCommon(r, s));
    }

    /// <summary>Tests references against OIDs.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (valueNode.InstanceType != "Reference") ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        if (string.IsNullOrEmpty(r.Reference))
        {
            return false;
        }

        return sp.ValueReferences.Any(s => CompareRefsOid(r, s));
    }


    /// <summary>Tests references against UUIDs.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (valueNode.InstanceType != "Reference") ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        Hl7.Fhir.Model.ResourceReference r = valueNode.ToPoco<Hl7.Fhir.Model.ResourceReference>();

        if (string.IsNullOrEmpty(r.Reference))
        {
            return false;
        }

        return sp.ValueReferences.Any(s => CompareRefsUuid(r, s));
    }

    /// <summary>Tests reference against primitive url types (canonical, uri, url).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstPrimitive(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueReferences == null))
        {
            return false;
        }

        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        int index = value.LastIndexOf('|');

        if (index != -1)
        {
            string cv = value.Substring(index + 1);
            string cu = value.Substring(0, index);

            return sp.ValueReferences.Any(s => 
                s.Url.Equals(cu, StringComparison.Ordinal) && 
                (string.IsNullOrEmpty(s.CanonicalVersion) || s.CanonicalVersion.Equals(cv, StringComparison.Ordinal)));
        }

        return sp.ValueReferences.Any(s => s.Url.Equals(value, StringComparison.Ordinal));
    }
}
