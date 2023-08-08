// <copyright file="EvalTokenSearch.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;
using Hl7.Fhir.ElementModel;

namespace FhirCandle.Search;

/// <summary>A class that contains functions to test token inputs against various FHIR types.</summary>
public static class EvalTokenSearch
{
    /// <summary>Compare code with system.</summary>
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

    /// <summary>Tests a token search value against string-type nodes, using exact matching (equality & case-sensitive).</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstStringValue(ITypedElement valueNode, ParsedSearchParameter sp)
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

    /// <summary>Tests a token search value against string-type nodes, using exact matching (case-sensitive), modified to 'not'.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenNotAgainstStringValue(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        if (string.IsNullOrEmpty(value))
        {
            // note that in 'not', missing values are matches
            return true;
        }

        for (int i = 0; i < sp.Values.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (sp.Values[i].Equals(value, StringComparison.Ordinal))
            {
                // not is inverted
                return false;
            }
        }

        // not is inverted
        return true;
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

        if (sp.ValueBools?.Any() ?? false)
        {
            for (int i = 0; i < sp.ValueBools.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.ValueBools[i] == value)
                {
                    return true;
                }
            }
        }
        else
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if ((value && sp.Values[i].StartsWith("t", StringComparison.OrdinalIgnoreCase)) ||
                    (!value && sp.Values[i].StartsWith("f", StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
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

        if (sp.ValueBools?.Any() ?? false)
        {
            for (int i = 0; i < sp.ValueBools.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if (sp.ValueBools[i] == value)
                {
                    // not is inverted
                    return false;
                }
            }
        }
        else
        {
            for (int i = 0; i < sp.Values.Length; i++)
            {
                if (sp.IgnoredValueFlags[i])
                {
                    continue;
                }

                if ((value && sp.Values[i].StartsWith("t", StringComparison.OrdinalIgnoreCase)) ||
                    (!value && sp.Values[i].StartsWith("f", StringComparison.OrdinalIgnoreCase)))
                {
                    // not is inverted
                    return false;
                }
            }
        }

        // not is inverted
        return true;
    }

    /// <summary>Tests token against code and coding types.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCoding(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        string valueSystem, valueCode;

        switch (valueNode.InstanceType)
        {
            case "Code":
                {
                    Hl7.Fhir.Model.Code v = valueNode.ToPoco<Hl7.Fhir.Model.Code>();

                    valueSystem = string.Empty;
                    valueCode = v.Value;
                }
                break;

            case "Coding":
                {
                    Hl7.Fhir.Model.Coding v = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Code ?? string.Empty;
                }
                break;

            case "Identifier":
                {
                    Hl7.Fhir.Model.Identifier v = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            case "ContactPoint":
                {
                    Hl7.Fhir.Model.ContactPoint v = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();

                    valueSystem = v.System?.ToString() ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            default:
                {
                    if ((valueNode.Value != null) &&
                        (valueNode.Value is string v))
                    {
                        valueSystem = string.Empty;
                        valueCode = v;
                    }
                    else
                    {
                        throw new Exception($"Cannot test token against type: {valueNode.InstanceType} as Coding");
                    }
                }
                break;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System ?? string.Empty, sp.ValueFhirCodes[i].Value))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Tests token against codeable concept.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public static bool TestTokenAgainstCodeableConcept(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            return false;
        }

        switch (valueNode.InstanceType)
        {
            case "CodeableConcept":
                {
                    Hl7.Fhir.Model.CodeableConcept cc = valueNode.ToPoco<Hl7.Fhir.Model.CodeableConcept>();

                    if (cc.Coding != null)
                    {
                        foreach (Hl7.Fhir.Model.Coding c in cc.Coding)
                        {
                            for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
                            {
                                if (sp.IgnoredValueFlags[i])
                                {
                                    continue;
                                }

                                if (CompareCodeWithSystem(
                                        c.System ?? string.Empty,
                                        c.Code ?? string.Empty,
                                        sp.ValueFhirCodes[i].System ?? string.Empty,
                                        sp.ValueFhirCodes[i].Value))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
                break;

            //case Hl7.Fhir.ElementModel.Types.Concept ec:
            //    {
            //        if (ec.Codes != null)
            //        {
            //            foreach (Hl7.Fhir.ElementModel.Types.Code c in ec.Codes)
            //            {
            //                if (sp.ValueFhirCodes.Any(v => CompareCodeWithSystem(
            //                        c.System ?? string.Empty,
            //                        c.Value ?? string.Empty,
            //                        v.System ?? string.Empty,
            //                        v.Value)))
            //                {
            //                    return true;
            //                }
            //            }
            //        }
            //    }
            //    break;

            default:
                throw new Exception($"Cannot test token against type: {valueNode.GetType()} as CodeableConcept");
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
        if ((valueNode == null) ||
            (sp.ValueFhirCodes == null))
        {
            // note that in 'not', missing values are matches
            return true;
        }

        string valueSystem, valueCode;

        switch (valueNode.InstanceType)
        {
            case "Code":
                {
                    Hl7.Fhir.Model.Code v = valueNode.ToPoco<Hl7.Fhir.Model.Code>();

                    valueSystem = string.Empty;
                    valueCode = v.Value;
                }
                break;

            case "Coding":
                {
                    Hl7.Fhir.Model.Coding v = valueNode.ToPoco<Hl7.Fhir.Model.Coding>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Code ?? string.Empty;
                }
                break;

            case "Identifier":
                {
                    Hl7.Fhir.Model.Identifier v = valueNode.ToPoco<Hl7.Fhir.Model.Identifier>();

                    valueSystem = v.System ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            case "ContactPoint":
                {
                    Hl7.Fhir.Model.ContactPoint v = valueNode.ToPoco<Hl7.Fhir.Model.ContactPoint>();

                    valueSystem = v.System?.ToString() ?? string.Empty;
                    valueCode = v.Value ?? string.Empty;
                }
                break;

            default:
                {
                    if ((valueNode.Value != null) &&
                        (valueNode.Value is string v))
                    {
                        valueSystem = string.Empty;
                        valueCode = v;
                    }
                    else
                    {
                        throw new Exception($"Cannot test token against type: {valueNode.InstanceType} as Coding");
                    }
                }
                break;
        }

        for (int i = 0; i < sp.ValueFhirCodes.Length; i++)
        {
            if (sp.IgnoredValueFlags[i])
            {
                continue;
            }

            if (CompareCodeWithSystem(valueSystem, valueCode, sp.ValueFhirCodes[i].System ?? string.Empty, sp.ValueFhirCodes[i].Value))
            {
                // not is inverted
                return false;
            }
        }

        // not is inverted
        return true;
    }
}
