// <copyright file="EvalTokenSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.ElementModel;

namespace FhirServerHarness.Search;

public static class EvalTokenSearch
{
    /// <summary>Units match.</summary>
    /// <param name="s1">The first system.</param>
    /// <param name="c1">The first code.</param>
    /// <param name="s2">The second system.</param>
    /// <param name="c2">The second code.</param>
    /// <returns>True if they match, false if they do not.</returns>
    private static bool CompareCodeWithSystem(string s1, string c1, string s2, string c2)
    {
        if (string.IsNullOrEmpty(s1) || 
            string.IsNullOrEmpty(s2) ||
            s1.Equals(s2, StringComparison.OrdinalIgnoreCase))
        {
            return c1.Equals(c2, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }


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
            // note that in 'not', missing values are matches
            return true;
        }

        return !sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal));
    }

    /// <summary>Tests token against bool.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstBool(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            return false;
        }

        bool value = (bool)valueNode.Value;

        if (sp.ValueBools != null)
        {
            if (sp.ValueBools.Contains(value))
            {
                return true;
            }
        }
        else                    // boolean values that got missed during search parameter parsing
        {
            if ((value && sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase))) ||
                (!value && sp.Values.Any(v => v.StartsWith("f", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token not against bool.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstBool(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            // note that in 'not', missing values are matches
            return true;
        }

        bool value = (bool)valueNode.Value;

        if (sp.ValueBools != null)
        {
            if (!sp.ValueBools.Contains(value))
            {
                return true;
            }
        }
        else                    // boolean values that got missed during search parameter parsing
        {
            if ((!value && sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase))) ||
                (value && sp.Values.Any(v => v.StartsWith("f", StringComparison.OrdinalIgnoreCase))))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token against code and coding types.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            return false;
        }

        string valueSystem, valueCode;

        switch (valueNode.Value)
        {
            case Hl7.Fhir.Model.Code fhirCode:
                valueSystem = string.Empty;
                valueCode = fhirCode.Value;
                break;

            case Hl7.Fhir.Model.Coding fhirCoding:
                valueSystem = fhirCoding.System ?? string.Empty;
                valueCode = fhirCoding.Code ?? string.Empty;
                break;

            case Hl7.Fhir.ElementModel.Types.Code fhirCode:
                valueSystem = fhirCode.System ?? string.Empty;
                valueCode = fhirCode.Value;
                break;

            case string codeString:
                valueSystem = string.Empty;
                valueCode = codeString;
                break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.Value.GetType()}");
        }

        if (sp.ValueFhirCodes == null)
        {
            return false;
        }

        if (sp.ValueFhirCodes.Any(v => CompareCodeWithSystem(valueSystem, valueCode, v.System ?? string.Empty, v.Value)))
        {
            return true;
        }

        return false;
    }

    /// <summary>Tests token not against coding.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if (valueNode?.Value == null)
        {
            // note that in 'not', missing values are matches
            return true;
        }

        string valueSystem, valueCode;

        switch (valueNode.Value)
        {
            case Hl7.Fhir.Model.Code fhirCode:
                valueSystem = string.Empty;
                valueCode = fhirCode.Value;
                break;

            case Hl7.Fhir.Model.Coding fhirCoding:
                valueSystem = fhirCoding.System ?? string.Empty;
                valueCode = fhirCoding.Code ?? string.Empty;
                break;

            case string codeString:
                valueSystem = string.Empty;
                valueCode = codeString;
                break;

            case Hl7.Fhir.ElementModel.Types.Code fhirCode:
                valueSystem = fhirCode.System ?? string.Empty;
                valueCode = fhirCode.Value;
                break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.Value.GetType()}");
        }

        if (sp.ValueFhirCodes == null)
        {
            return false;
        }

        if (sp.ValueFhirCodes.Any(v => CompareCodeWithSystem(valueSystem, valueCode, v.System ?? string.Empty, v.Value)))
        {
            return false;
        }

        return true;
    }
}
