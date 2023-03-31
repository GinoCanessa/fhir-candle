// <copyright file="EvalTokenSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;

namespace FhirServerHarness.Search;

public static class EvalTokenSearch
{
    /// <summary>Tests a token search value against id-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstId(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal));
    }

    /// <summary>Tests a token search value against id-type nodes, using exact matching (case-sensitive), modified to 'not'.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstId(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return !sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal));
    }

}
