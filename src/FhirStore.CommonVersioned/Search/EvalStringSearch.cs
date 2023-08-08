// <copyright file="SearchTestString.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;
using static FhirCandle.Search.SearchDefinitions;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test string inputs against various FHIR types.</summary>
public static class EvalStringSearch
{
    /// <summary>Tests a string search value against string-type nodes, using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWith(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].StartsWith(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against string-type nodes, using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContains(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Contains(value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExact(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(value, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using starts-with & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringStartsWithAgainstHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode == null)
        {
            return false;
        }

        Hl7.Fhir.Model.HumanName hn = valueNode.ToPoco<HumanName>();

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((hn.Family?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (hn.Given?.Any(gn => gn.StartsWith(v, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                (hn.Text?.StartsWith(v, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using contains & case-insensitive.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringContainsAgainstHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode == null)
        {
            return false;
        }

        Hl7.Fhir.Model.HumanName hn = valueNode.ToPoco<HumanName>();

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((hn.Family?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (hn.Given?.Any(gn => gn.Contains(v, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                (hn.Text?.Contains(v, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests a string search value against a human name (family, given, or text), using exact matching (case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestStringExactAgainstHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode == null)
        {
            return false;
        }

        Hl7.Fhir.Model.HumanName hn = valueNode.ToPoco<HumanName>();

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            string v = sp.Values[i];

            if ((hn.Family?.Equals(v, StringComparison.Ordinal) ?? false) ||
                (hn.Given?.Any(gn => gn.Equals(v, StringComparison.Ordinal)) ?? false) ||
                (hn.Text?.Equals(v, StringComparison.Ordinal) ?? false))
            {
                return true;
            }
        }

        return false;
    }
}
