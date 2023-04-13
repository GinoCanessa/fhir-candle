// <copyright file="EvalUriSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;

namespace FhirServerHarness.Search;

/// <summary>A class that contains functions to test URI inputs against various FHIR types.</summary>
public static class EvalUriSearch
{

    /// <summary>Tests a token search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstStringValue(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal));
    }

    /// <summary>Tests uri values against OIDs.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstOid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("urn:oid:", StringComparison.Ordinal))
        {
            return sp.Values.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                                    ("urn:oid:" + v).Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        return sp.Values.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                                v.Equals("urn:oid:" + value, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Tests uri values against UUIDs.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestUriAgainstUuid(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (value.StartsWith("urn:uuid:", StringComparison.Ordinal))
        {
            return sp.Values.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                                    ("urn:uuid:" + v).Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        return sp.Values.Any(v => v.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                                v.Equals("urn:uuid:" + value, StringComparison.OrdinalIgnoreCase));
    }

}
