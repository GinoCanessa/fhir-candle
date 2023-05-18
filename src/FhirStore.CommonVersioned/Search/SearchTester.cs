// <copyright file="SearchTester.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Extensions;
using FhirStore.Models;
using FhirStore.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Newtonsoft.Json.Linq;
using System.Globalization;
using static fhir.candle.Search.SearchDefinitions;

namespace fhir.candle.Search;

/// <summary>Test parsed search parameters against resources.</summary>
public class SearchTester
{
    /// <summary>Gets or sets the store.</summary>
    public required VersionedFhirStore FhirStore { get; init; }

    /// <summary>Tests a resource against parsed search parameters for matching.</summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="resource">         The resource.</param>
    /// <param name="searchParameters"> Options for controlling the search.</param>
    /// <param name="appliedParameters">[out] Options for controlling the applied.</param>
    /// <param name="ignoredParameters">[out] Options for controlling the ignored.</param>
    /// <param name="fpContext">        (Optional) The context.</param>
    /// <returns>True if the test passes, false if the test fails.</returns>
    public bool TestForMatch(
        ITypedElement resource,
        IEnumerable<ParsedSearchParameter> searchParameters,
        FhirEvaluationContext? fpContext = null)
    {
        if (resource == null)
        {
            throw new ArgumentNullException(nameof(resource));
        }

        if (!searchParameters.Any())
        {
            return true;
        }

        if (fpContext == null)
        {
            fpContext = new FhirEvaluationContext(resource.ToScopedNode());
            fpContext.ElementResolver = FhirStore.Resolve;
        }

        foreach (ParsedSearchParameter sp in searchParameters)
        {
            if (sp.IgnoredParameter)
            {
                continue;
            }

            if (sp.ParamType == SearchParamType.Composite)
            {
                // TODO: Firely is missing composite definitions, need to either load the packages myself or submit fixes
                //ignored.Add(sp);
                continue;

#pragma warning disable CS0162 // Unreachable code detected
                if (sp.CompositeComponents == null)
                {
                    continue;
                }

                // component matching is all or nothing
                if (TestForMatch(
                        resource,
                        searchParameters,
                        fpContext))
                {
                    continue;
                }

                return false;
#pragma warning restore CS0162 // Unreachable code detected
            }

            if (sp.CompiledExpression == null)
            {
                // TODO: Handle non-trivial search parameters
                continue;
            }

            // check for unsupported modifiers
            if ((!string.IsNullOrEmpty(sp.ModifierLiteral)) &&
                (!IsModifierValidForType(sp.Modifier, sp.ParamType)))
            {
                continue;
            }

            IEnumerable<ITypedElement> extracted = sp.CompiledExpression.Invoke(resource, fpContext);

            if (!extracted.Any())
            {
                if ((sp.Modifier == SearchModifierCodes.Missing) &&
                    sp.Values.Any(v => v.StartsWith("t", StringComparison.OrdinalIgnoreCase)))
                {
                    // successful match
                    continue;
                }

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
                string combined = sp.Modifier == SearchModifierCodes.None 
                    ? $"{sp.ParamType}-{resultNode.InstanceType}".ToLowerInvariant()
                    : $"{sp.ParamType}-{sp.Modifier}-{resultNode.InstanceType}".ToLowerInvariant();

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

                    case "reference-uri":
                    case "reference-url":
                    case "reference-canonical":
                        if (EvalReferenceSearch.TestReferenceAgainstPrimitive(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "reference-reference":
                        if (EvalReferenceSearch.TestReference(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "reference-oid":
                        if (EvalReferenceSearch.TestReferenceAgainstOid(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "reference-uuid":
                        if (EvalReferenceSearch.TestReferenceAgainstUuid(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
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

                    case "token-canonical":
                    case "token-id":
                    case "token-oid":
                    case "token-uri":
                    case "token-url":
                    case "token-uuid":
                    case "token-string":
                        if (EvalTokenSearch.TestTokenAgainstStringValue(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-not-canonical":
                    case "token-not-id":
                    case "token-not-oid":
                    case "token-not-uri":
                    case "token-not-url":
                    case "token-not-uuid":
                    case "token-not-string":
                        if (EvalTokenSearch.TestTokenNotAgainstStringValue(resultNode, sp))
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
                    case "token-contactpoint":
                    case "token-identifier":
                        if (EvalTokenSearch.TestTokenAgainstCoding(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-not-code":
                    case "token-not-coding":
                    case "token-not-contactpoint":
                    case "token-not-identifier":
                        if (EvalTokenSearch.TestTokenNotAgainstCoding(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "token-codeableconcept":
                        if (EvalTokenSearch.TestTokenAgainstCodeableConcept(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "uri-canonical":
                    case "uri-uri":
                    case "uri-url":
                        if (EvalUriSearch.TestUriAgainstStringValue(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "uri-oid":
                        if (EvalUriSearch.TestUriAgainstOid(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "uri-uuid":
                        if (EvalUriSearch.TestUriAgainstUuid(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    // note: this is a special case, the literals used are 'actual' resource types (e.g., patient)
                    case "reference-resourcetype-canonical":
                    case "reference-resourcetype-reference":
                    case "reference-resourcetype-oid":
                    case "reference-resourcetype-uri":
                    case "reference-resourcetype-url":
                    case "reference-resourcetype-uuid":
                        break;

                    case "reference-above-canonical":
                    case "reference-above-reference":
                    case "reference-above-oid":
                    case "reference-above-uri":
                    case "reference-above-url":
                    case "reference-above-uuid":
                    case "reference-below-canonical":
                    case "reference-below-reference":
                    case "reference-below-oid":
                    case "reference-below-uri":
                    case "reference-below-url":
                    case "reference-below-uuid":
                    case "reference-codetext-canonical":
                    case "reference-codetext-reference":
                    case "reference-codetext-oid":
                    case "reference-codetext-uri":
                    case "reference-codetext-url":
                    case "reference-codetext-uuid":
                    case "reference-identifier-canonical":
                    case "reference-identifier-reference":
                    case "reference-identifier-oid":
                    case "reference-identifier-uri":
                    case "reference-identifier-url":
                    case "reference-identifier-uuid":
                    case "reference-in-canonical":
                    case "reference-in-reference":
                    case "reference-in-oid":
                    case "reference-in-uri":
                    case "reference-in-url":
                    case "reference-in-uuid":
                    case "reference-notin-canonical":
                    case "reference-notin-reference":
                    case "reference-notin-oid":
                    case "reference-notin-uri":
                    case "reference-notin-url":
                    case "reference-notin-uuid":
                    case "reference-text-canonical":
                    case "reference-text-reference":
                    case "reference-text-oid":
                    case "reference-text-uri":
                    case "reference-text-url":
                    case "reference-text-uuid":
                    case "reference-textadvanced-canonical":
                    case "reference-textadvanced-reference":
                    case "reference-textadvanced-oid":
                    case "reference-textadvanced-uri":
                    case "reference-textadvanced-url":
                    case "reference-textadvanced-uuid":
                    case "token-above-code":
                    case "token-above-coding":
                    case "token-below-code":
                    case "token-below-coding":
                    case "token-codetext-code":
                    case "token-codetext-coding":
                    case "token-in-code":
                    case "token-in-coding":
                    case "token-notin-code":
                    case "token-notin-coding":
                    case "token-oftype-code":
                    case "token-oftype-coding":
                    case "token-text-code":
                    case "token-text-coding":
                    case "token-textadvanced-code":
                    case "token-textadvanced-coding":
                    case "token-above-codeableconcept":
                    case "token-above-identifier":
                    case "token-above-contactpoint":
                    case "token-above-canonical":
                    case "token-above-oid":
                    case "token-above-uri":
                    case "token-above-url":
                    case "token-above-uuid":
                    case "token-above-string":
                    case "token-below-codeableconcept":
                    case "token-below-identifier":
                    case "token-below-contactpoint":
                    case "token-below-canonical":
                    case "token-below-oid":
                    case "token-below-uri":
                    case "token-below-url":
                    case "token-below-uuid":
                    case "token-below-string":
                    case "token-codetext-codeableconcept":
                    case "token-codetext-identifier":
                    case "token-codetext-contactpoint":
                    case "token-codetext-canonical":
                    case "token-codetext-oid":
                    case "token-codetext-uri":
                    case "token-codetext-url":
                    case "token-codetext-uuid":
                    case "token-codetext-string":
                    case "token-in-codeableconcept":
                    case "token-in-identifier":
                    case "token-in-contactpoint":
                    case "token-in-canonical":
                    case "token-in-oid":
                    case "token-in-uri":
                    case "token-in-url":
                    case "token-in-uuid":
                    case "token-in-string":
                    case "token-not-codeableconcept":
                    case "token-notin-codeableconcept":
                    case "token-notin-identifier":
                    case "token-notin-contactpoint":
                    case "token-notin-canonical":
                    case "token-notin-oid":
                    case "token-notin-uri":
                    case "token-notin-url":
                    case "token-notin-uuid":
                    case "token-notin-string":
                    case "token-oftype-codeableconcept":
                    case "token-oftype-identifier":
                    case "token-oftype-contactpoint":
                    case "token-oftype-canonical":
                    case "token-oftype-oid":
                    case "token-oftype-uri":
                    case "token-oftype-url":
                    case "token-oftype-uuid":
                    case "token-oftype-string":
                    case "token-text-codeableconcept":
                    case "token-text-identifier":
                    case "token-text-contactpoint":
                    case "token-text-canonical":
                    case "token-text-oid":
                    case "token-text-uri":
                    case "token-text-url":
                    case "token-text-uuid":
                    case "token-text-string":
                    case "token-textadvanced-codeableconcept":
                    case "token-textadvanced-identifier":
                    case "token-textadvanced-contactpoint":
                    case "token-textadvanced-canonical":
                    case "token-textadvanced-oid":
                    case "token-textadvanced-uri":
                    case "token-textadvanced-url":
                    case "token-textadvanced-uuid":
                    case "token-textadvanced-string":
                    case "uri-above-canonical":
                    case "uri-above-oid":
                    case "uri-above-uri":
                    case "uri-above-url":
                    case "uri-above-uuid":
                    case "uri-below-canonical":
                    case "uri-below-oid":
                    case "uri-below-uri":
                    case "uri-below-url":
                    case "uri-below-uuid":
                    case "uri-contains-canonical":
                    case "uri-contains-oid":
                    case "uri-contains-uri":
                    case "uri-contains-url":
                    case "uri-contains-uuid":
                    case "uri-in-canonical":
                    case "uri-in-oid":
                    case "uri-in-uri":
                    case "uri-in-url":
                    case "uri-in-uuid":
                    case "uri-not-canonical":
                    case "uri-not-oid":
                    case "uri-not-uri":
                    case "uri-not-url":
                    case "uri-not-uuid":
                    case "uri-notin-canonical":
                    case "uri-notin-oid":
                    case "uri-notin-uri":
                    case "uri-notin-url":
                    case "uri-notin-uuid":
                    case "uri-oftype-canonical":
                    case "uri-oftype-oid":
                    case "uri-oftype-uri":
                    case "uri-oftype-url":
                    case "uri-oftype-uuid":
                    case "uri-text-canonical":
                    case "uri-text-oid":
                    case "uri-text-uri":
                    case "uri-text-url":
                    case "uri-text-uuid":
                    case "uri-textadvanced-canonical":
                    case "uri-textadvanced-oid":
                    case "uri-textadvanced-uri":
                    case "uri-textadvanced-url":
                    case "uri-textadvanced-uuid":
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
                return false;
            }
        }

        // succesfully matching all parameters means this resource is a match
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
