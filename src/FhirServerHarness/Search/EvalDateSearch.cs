// <copyright file="EvalDateSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;
using static FhirServerHarness.Search.SearchDefinitions;

namespace FhirServerHarness.Search;

public static class EvalDateSearch
{

    /// <summary>Performs a search test for a date type.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool TestDate(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        switch (sp.Modifier)
        {
            case SearchModifierCodes.None:
                if (valueNode?.Value == null)
                {
                    return false;
                }




                decimal elementValue = (decimal)(valueNode?.Value ?? 0);

                // traverse values and possibly prefixes
                for (int i = 0; i < sp.Values.Length; i++)
                {
                    // either grab the prefix or default to equality (number default prefix is equality)
                    SearchPrefixCodes prefix =
                        ((sp.Prefixes?.Length ?? 0) > i)
                        ? sp.Prefixes![i] ?? SearchPrefixCodes.Equal
                        : SearchPrefixCodes.Equal;

                    if (!decimal.TryParse(sp.Values[i], out decimal testValue))
                    {
                        continue;
                    }

                    switch (prefix)
                    {
                        case SearchPrefixCodes.Equal:
                        default:

                            // TODO: This is not proper decimal comparison as defined in the spec.

                            //int digits = testValue.GetSignificantDigitCount();

                            if (decimal.Abs(decimal.Round(elementValue - testValue, 0)) == 0)
                            {
                                return true;
                            }

                            break;

                        case SearchPrefixCodes.NotEqual:

                            // TODO: This is not proper decimal comparison as defined in the spec.
                            if (decimal.Abs(decimal.Round(elementValue - testValue, 0)) != 0)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.GreaterThan:
                            if (elementValue > testValue)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.LessThan:
                            if (elementValue < testValue)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.GreaterThanOrEqual:
                            if (elementValue >= testValue)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.LessThanOrEqual:
                            if (elementValue <= testValue)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.StartsAfter:
                            if (elementValue > testValue)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.EndsBefore:
                            if (elementValue < testValue)
                            {
                                return true;
                            }
                            break;

                        case SearchPrefixCodes.Approximately:

                            // values within 10% should match
                            if ((decimal.Abs(elementValue - testValue) / decimal.Abs(elementValue)) < 0.1m)
                            {
                                return true;
                            }
                            break;
                    }
                }

                // if we did not find a match, this test failed
                return false;

            case SearchModifierCodes.Missing:
                return sp.Values.Any(v =>
                    (v.StartsWith("t", StringComparison.OrdinalIgnoreCase) && (valueNode?.Value == null)) ||
                    (v.StartsWith("f", StringComparison.OrdinalIgnoreCase) && (valueNode?.Value != null)));

            case SearchModifierCodes.ResourceType:
            case SearchModifierCodes.Above:
            case SearchModifierCodes.Below:
            case SearchModifierCodes.CodeText:
            case SearchModifierCodes.Contains:
            case SearchModifierCodes.Exact:
            case SearchModifierCodes.Identifier:
            case SearchModifierCodes.In:
            case SearchModifierCodes.Iterate:
            case SearchModifierCodes.Not:
            case SearchModifierCodes.NotIn:
            case SearchModifierCodes.OfType:
            case SearchModifierCodes.Text:
            case SearchModifierCodes.TextAdvanced:
            default:
                throw new Exception($"Invalid search modifier for number: {sp.ModifierLiteral}");
        }
    }

}
