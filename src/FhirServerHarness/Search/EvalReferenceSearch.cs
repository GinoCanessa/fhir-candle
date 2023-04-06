// <copyright file="EvalReferenceSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;

namespace FhirServerHarness.Search;

/// <summary>A class that contains functions to test reference inputs against various FHIR types.</summary>
public static class EvalReferenceSearch
{
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

    /// <summary>Tests reference against canonical.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestReferenceAgainstCanonical(ITypedElement valueNode, ParsedSearchParameter sp)
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

        int index = r.Reference.LastIndexOf('|');

        if (index != -1)
        {
            string cv = r.Reference.Substring(index + 1);
            string cu = r.Reference.Substring(0, index);

            return sp.ValueReferences.Any(s => 
                s.Url.Equals(cu, StringComparison.Ordinal) && 
                (string.IsNullOrEmpty(s.CanonicalVersion) || s.CanonicalVersion.Equals(cv, StringComparison.Ordinal)));
        }

        return sp.ValueReferences.Any(s => s.Url.Equals(r.Reference, StringComparison.Ordinal));
    }
}
