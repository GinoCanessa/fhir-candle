// <copyright file="SearchTester.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Extensions;
using FhirServerHarness.Models;
using FhirServerHarness.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Newtonsoft.Json.Linq;
using System.Globalization;
using static FhirServerHarness.Search.SearchDefinitions;

namespace FhirServerHarness.Search;

/// <summary>Test parsed search parameters against resources.</summary>
public class SearchTester
{
    /// <summary>Gets or sets the store.</summary>
    public required IFhirStore FhirStore { get; set; }

    /// <summary>Tests a resource against parsed search parameters for matching.</summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="resource">         The resource.</param>
    /// <param name="searchParameters"> Options for controlling the search.</param>
    /// <param name="appliedParameters">[out] Options for controlling the applied.</param>
    /// <param name="ignoredParameters">[out] Options for controlling the ignored.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public bool TestForMatch(
        ITypedElement resource,
        IEnumerable<ParsedSearchParameter> searchParameters,
        out IEnumerable<ParsedSearchParameter> appliedParameters,
        out IEnumerable<ParsedSearchParameter> ignoredParameters)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (!searchParameters.Any())
        {
            appliedParameters = Array.Empty<ParsedSearchParameter>();
            ignoredParameters = Array.Empty<ParsedSearchParameter>();
            return true;
        }

        List<ParsedSearchParameter> applied = new();
        List<ParsedSearchParameter> ignored = new();

        FhirEvaluationContext fpContext = new FhirEvaluationContext(resource.ToScopedNode());
        fpContext.ElementResolver = FhirStore.Resolve;

        foreach (ParsedSearchParameter sp in searchParameters)
        {
            if (string.IsNullOrEmpty(sp.SelectExpression))
            {
                // TODO: Handle non-trivial search parameters
                ignored.Add(sp);
                continue;
            }

            // check for unsupported modifiers
            if ((!string.IsNullOrEmpty(sp.ModifierLiteral)) &&
                (!IsModifierValidForType(sp.Modifier, sp.ParamType)))
            {
                ignored.Add(sp);
                continue;
            }

            applied.Add(sp);

            IEnumerable<ITypedElement> extracted = resource.Select(sp.SelectExpression, fpContext);

            if (!extracted.Any())
            {
                if ((sp.Modifier == SearchModifierCodes.Missing) &&
                    sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase)))
                {
                    // successful match
                    continue;
                }

                appliedParameters = applied;
                ignoredParameters = ignored;
                return false;
            }

            bool found = false;

            foreach (ITypedElement resultNode in extracted)
            {
                // all types evaluate missing the same way
                if (sp.Modifier == SearchModifierCodes.Missing)
                {
                    if (SearchTestMissing(resultNode, sp))
                    {
                        found = true;
                        break;
                    }

                    continue;
                }

                // build a routing tuple: {search type}<-{modifier}>-{value type}
                string combined = string.IsNullOrEmpty(sp.ModifierLiteral)
                    ? $"{sp.ParamType}-{resultNode.InstanceType}".ToLowerInvariant()
                    : $"{sp.ParamType}-{sp.ModifierLiteral}-{resultNode.InstanceType}".ToLowerInvariant();

                // this switch is intentionally 'unrolled' for performance (instead of nesting by type)
                // the 'missing' modifier is handled earlier so never appears in this switch
                switch (combined)
                {
                    case "date-date":
                    case "date-datetime":
                    case "date-instant":
                    case "date-period":
                    case "date-timing":
                        if (EvalDateSearch.TestDate(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;


                    // note that the SDK keeps all ITypedElement 'integer' values in 64-bit format
                    case "number-integer":
                    case "number-unsignedint":
                    case "number-positiveint":
                    case "number-integer64":
                        if (EvalNumberSearch.TestNumberAgainstLong(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "number-decimal":
                        if (EvalNumberSearch.TestNumberAgainstDecimal(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "quantity-quantity":
                        if (EvalQuantitySearch.TestQuantity(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "reference-canonical":
                        if (EvalReferenceSearch.TestReferenceAgainstCanonical(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "reference-reference":
                    case "reference-oid":
                    case "reference-uri":
                    case "reference-url":
                    case "reference-uuid":
                        if (EvalReferenceSearch.TestReference(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "reference-above-canonical":
                    case "reference-above-reference":
                    case "reference-above-oid":
                    case "reference-above-uri":
                    case "reference-above-url":
                    case "reference-above-uuid":
                        break;

                    case "reference-below-canonical":
                    case "reference-below-reference":
                    case "reference-below-oid":
                    case "reference-below-uri":
                    case "reference-below-url":
                    case "reference-below-uuid":
                        break;

                    case "reference-code-text-canonical":
                    case "reference-code-text-reference":
                    case "reference-code-text-oid":
                    case "reference-code-text-uri":
                    case "reference-code-text-url":
                    case "reference-code-text-uuid":
                        break;

                    case "reference-identifier-canonical":
                    case "reference-identifier-reference":
                    case "reference-identifier-oid":
                    case "reference-identifier-uri":
                    case "reference-identifier-url":
                    case "reference-identifier-uuid":
                        break;

                    case "reference-in-canonical":
                    case "reference-in-reference":
                    case "reference-in-oid":
                    case "reference-in-uri":
                    case "reference-in-url":
                    case "reference-in-uuid":
                        break;

                    case "reference-not-in-canonical":
                    case "reference-not-in-reference":
                    case "reference-not-in-oid":
                    case "reference-not-in-uri":
                    case "reference-not-in-url":
                    case "reference-not-in-uuid":
                        break;

                    case "reference-text-canonical":
                    case "reference-text-reference":
                    case "reference-text-oid":
                    case "reference-text-uri":
                    case "reference-text-url":
                    case "reference-text-uuid":
                        break;

                    case "reference-text-advanced-canonical":
                    case "reference-text-advanced-reference":
                    case "reference-text-advanced-oid":
                    case "reference-text-advanced-uri":
                    case "reference-text-advanced-url":
                    case "reference-text-advanced-uuid":
                        break;

                    // note: this is a special case, the literals used are 'actual' resource types (e.g., patient)
                    case "reference-rt-canonical":
                    case "reference-rt-reference":
                    case "reference-rt-oid":
                    case "reference-rt-uri":
                    case "reference-rt-url":
                    case "reference-rt-uuid":
                        break;

                    case "string-id":
                    case "string-string":
                    case "string-markdown":
                    case "string-xhtml":
                        if (EvalStringSearch.TestStringStartsWith(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "string-contains-id":
                    case "string-contains-string":
                    case "string-contains-markdown":
                    case "string-contains-xhtml":
                        if (EvalStringSearch.TestStringContains(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "string-exact-id":
                    case "string-exact-string":
                    case "string-exact-markdown":
                    case "string-exact-xhtml":
                        if (EvalStringSearch.TestStringExact(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "string-humanname":
                        if (EvalStringSearch.TestStringStartsWithAgainstHumanName(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "string-contains-humanname":
                        if (EvalStringSearch.TestStringContainsAgainstHumanName(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "string-exact-humanname":
                        if (EvalStringSearch.TestStringExactAgainstHumanName(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-id":
                        if (EvalTokenSearch.TestTokenAgainstId(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-not-id":
                        if (EvalTokenSearch.TestTokenNotAgainstId(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-boolean":
                        if (EvalTokenSearch.TestTokenAgainstBool(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-not-boolean":
                        if (EvalTokenSearch.TestTokenNotAgainstBool(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-code":
                    case "token-coding":
                        if (EvalTokenSearch.TestTokenAgainstCoding(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-not-code":
                    case "token-not-coding":
                        if (EvalTokenSearch.TestTokenNotAgainstCoding(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-above-code":
                    case "token-above-coding":
                        break;

                    case "token-below-code":
                    case "token-below-coding":
                        break;

                    case "token-code-text-code":
                    case "token-code-text-coding":
                        break;

                    case "token-in-code":
                    case "token-in-coding":
                        break;

                    case "token-not-in-code":
                    case "token-not-in-coding":
                        break;

                    case "token-of-type-code":
                    case "token-of-type-coding":
                        break;

                    case "token-text-code":
                    case "token-text-coding":
                        break;

                    case "token-text-advanced-code":
                    case "token-text-advanced-coding":
                        break;

                    case "token-codeableconcept":
                    case "token-identifier":
                    case "token-contactpoint":
                    case "token-canonical":
                    case "token-oid":
                    case "token-uri":
                    case "token-url":
                    case "token-uuid":
                    case "token-string":
                        break;

                    case "token-above-codeableconcept":
                    case "token-above-identifier":
                    case "token-above-contactpoint":
                    case "token-above-canonical":
                    case "token-above-oid":
                    case "token-above-uri":
                    case "token-above-url":
                    case "token-above-uuid":
                    case "token-above-string":
                        break;

                    case "token-below-codeableconcept":
                    case "token-below-identifier":
                    case "token-below-contactpoint":
                    case "token-below-canonical":
                    case "token-below-oid":
                    case "token-below-uri":
                    case "token-below-url":
                    case "token-below-uuid":
                    case "token-below-string":
                        break;

                    case "token-code-text-codeableconcept":
                    case "token-code-text-identifier":
                    case "token-code-text-contactpoint":
                    case "token-code-text-canonical":
                    case "token-code-text-oid":
                    case "token-code-text-uri":
                    case "token-code-text-url":
                    case "token-code-text-uuid":
                    case "token-code-text-string":
                        break;

                    case "token-in-codeableconcept":
                    case "token-in-identifier":
                    case "token-in-contactpoint":
                    case "token-in-canonical":
                    case "token-in-oid":
                    case "token-in-uri":
                    case "token-in-url":
                    case "token-in-uuid":
                    case "token-in-string":
                        break;

                    case "token-not-codeableconcept":
                    case "token-not-identifier":
                    case "token-not-contactpoint":
                    case "token-not-canonical":
                    case "token-not-oid":
                    case "token-not-uri":
                    case "token-not-url":
                    case "token-not-uuid":
                    case "token-not-string":
                        break;

                    case "token-not-in-codeableconcept":
                    case "token-not-in-identifier":
                    case "token-not-in-contactpoint":
                    case "token-not-in-canonical":
                    case "token-not-in-oid":
                    case "token-not-in-uri":
                    case "token-not-in-url":
                    case "token-not-in-uuid":
                    case "token-not-in-string":
                        break;

                    case "token-of-type-codeableconcept":
                    case "token-of-type-identifier":
                    case "token-of-type-contactpoint":
                    case "token-of-type-canonical":
                    case "token-of-type-oid":
                    case "token-of-type-uri":
                    case "token-of-type-url":
                    case "token-of-type-uuid":
                    case "token-of-type-string":
                        break;

                    case "token-text-codeableconcept":
                    case "token-text-identifier":
                    case "token-text-contactpoint":
                    case "token-text-canonical":
                    case "token-text-oid":
                    case "token-text-uri":
                    case "token-text-url":
                    case "token-text-uuid":
                    case "token-text-string":
                        break;

                    case "token-text-advanced-codeableconcept":
                    case "token-text-advanced-identifier":
                    case "token-text-advanced-contactpoint":
                    case "token-text-advanced-canonical":
                    case "token-text-advanced-oid":
                    case "token-text-advanced-uri":
                    case "token-text-advanced-url":
                    case "token-text-advanced-uuid":
                    case "token-text-advanced-string":
                        break;

                    case "uri-canonical":
                    case "uri-oid":
                    case "uri-uri":
                    case "uri-url":
                    case "uri-uuid":
                        break;

                    case "uri-above-canonical":
                    case "uri-above-oid":
                    case "uri-above-uri":
                    case "uri-above-url":
                    case "uri-above-uuid":
                        break;

                    case "uri-below-canonical":
                    case "uri-below-oid":
                    case "uri-below-uri":
                    case "uri-below-url":
                    case "uri-below-uuid":
                        break;

                    case "uri-contains-canonical":
                    case "uri-contains-oid":
                    case "uri-contains-uri":
                    case "uri-contains-url":
                    case "uri-contains-uuid":
                        break;

                    case "uri-in-canonical":
                    case "uri-in-oid":
                    case "uri-in-uri":
                    case "uri-in-url":
                    case "uri-in-uuid":
                        break;

                    case "uri-not-canonical":
                    case "uri-not-oid":
                    case "uri-not-uri":
                    case "uri-not-url":
                    case "uri-not-uuid":
                        break;

                    case "uri-not-in-canonical":
                    case "uri-not-in-oid":
                    case "uri-not-in-uri":
                    case "uri-not-in-url":
                    case "uri-not-in-uuid":
                        break;

                    case "uri-of-type-canonical":
                    case "uri-of-type-oid":
                    case "uri-of-type-uri":
                    case "uri-of-type-url":
                    case "uri-of-type-uuid":
                        break;

                    case "uri-text-canonical":
                    case "uri-text-oid":
                    case "uri-text-uri":
                    case "uri-text-url":
                    case "uri-text-uuid":
                        break;

                    case "uri-text-advanced-canonical":
                    case "uri-text-advanced-oid":
                    case "uri-text-advanced-uri":
                    case "uri-text-advanced-url":
                    case "uri-text-advanced-uuid":
                        break;

                    // Note that there is no defined way to search for a time
                    //case "date-time":
                    default:
                        break;
                }
            }
            
            if (!found)
            {
                // no matches in any extracted value means a parameter did NOT match
                appliedParameters = applied;
                ignoredParameters = ignored;
                return false;
            }
        }

        // succesfully matching all parameters means this resource is a match

        appliedParameters = applied;
        ignoredParameters = ignored;
        return true;
    }


    /// <summary>Searches for the first test missing.</summary>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public static bool SearchTestMissing(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        bool positive = sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase));
        bool negative = sp.Values.Any(v => v.StartsWith("f", StringComparison.OrdinalIgnoreCase));

        // testing both missing and not missing is always true
        if (positive && negative)
        {
            return true;
        }

        // test for missing and a null value
        if (positive && (valueNode?.Value == null))
        {
            return true;
        }

        // test for not missing and not a null value
        if (negative && (valueNode?.Value != null))
        {
            return true;
        }

        // other combinations are search misses
        return false;
    }

    /// <summary>Performs a search test against a human name.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool SearchTestHumanName(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        foreach (ITypedElement node in valueNode.Descendants())
        { 
            if (node.InstanceType != "string")
            {
                continue;
            }
            string value = (string)(node?.Value ?? string.Empty);

            switch (sp.Modifier)
            {
                case SearchModifierCodes.None:
                    {
                        if (sp.Values.Any(v => value.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Contains:
                    {
                        if (sp.Values.Any(v => value.Contains(v, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Exact:
                    {
                        if (sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Missing:
                    {
                        if (sp.Values.Any(v =>
                            (v.StartsWith("t", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value)) ||
                            (v.StartsWith("f", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.Not:
                    {
                        if (sp.Values.Any(v => !value.Equals(v, StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                    break;

                case SearchModifierCodes.ResourceType:
                case SearchModifierCodes.Above:
                case SearchModifierCodes.Below:
                case SearchModifierCodes.CodeText:
                case SearchModifierCodes.Identifier:
                case SearchModifierCodes.In:
                case SearchModifierCodes.Iterate:
                case SearchModifierCodes.NotIn:
                case SearchModifierCodes.OfType:
                case SearchModifierCodes.Text:
                case SearchModifierCodes.TextAdvanced:
                default:
                    throw new Exception($"Invalid search modifier for HumanName: {sp.ModifierLiteral}");
            }
        }

        return false;
    }
}
