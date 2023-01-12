// <copyright file="SearchTester.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using FhirServerHarness.Storage;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;

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

            applied.Add(sp);

            IEnumerable<ITypedElement> extracted = resource.Select(sp.SelectExpression, fpContext);

            if (!extracted.Any())
            {
                if ((sp.Modifier == ParsedSearchParameter.SearchModifierCodes.Missing) &&
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
                switch (resultNode.InstanceType)
                {
                    case "id":
                    case "string":
                        if (SearchTestString(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
                        break;

                    case "HumanName":
                        if (SearchTestHumanName(resultNode, sp))
                        {
                            found = true;
                            break;
                        }
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

    /// <summary>Performs a search test against a string.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    /// <param name="valueNode">The value node.</param>
    /// <param name="sp">       The sp.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private static bool SearchTestString(ITypedElement valueNode, ParsedSearchParameter sp)
    {
        string value = (string)(valueNode?.Value ?? string.Empty);

        switch (sp.Modifier)
        {
            case ParsedSearchParameter.SearchModifierCodes.None:
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        return false;
                    }

                    return sp.Values.Any(v => value.StartsWith(v, StringComparison.OrdinalIgnoreCase));
                }

            case ParsedSearchParameter.SearchModifierCodes.Contains:
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        return false;
                    }

                    return sp.Values.Any(v => value.Contains(v, StringComparison.OrdinalIgnoreCase));
                }

            case ParsedSearchParameter.SearchModifierCodes.Exact:
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        return false;
                    }

                    return sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal));
                }

            case ParsedSearchParameter.SearchModifierCodes.Missing:
                {
                    return sp.Values.Any(v => 
                        (v.StartsWith("t", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value)) ||
                        (v.StartsWith("f", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value)));
                }

            case ParsedSearchParameter.SearchModifierCodes.Not:
                {
                    return sp.Values.Any(v => !value.Equals(v, StringComparison.Ordinal));
                }

            case ParsedSearchParameter.SearchModifierCodes.ResourceType:
            case ParsedSearchParameter.SearchModifierCodes.Above:
            case ParsedSearchParameter.SearchModifierCodes.Below:
            case ParsedSearchParameter.SearchModifierCodes.CodeText:
            case ParsedSearchParameter.SearchModifierCodes.Identifier:
            case ParsedSearchParameter.SearchModifierCodes.In:
            case ParsedSearchParameter.SearchModifierCodes.Iterate:
            case ParsedSearchParameter.SearchModifierCodes.NotIn:
            case ParsedSearchParameter.SearchModifierCodes.OfType:
            case ParsedSearchParameter.SearchModifierCodes.Text:
            case ParsedSearchParameter.SearchModifierCodes.TextAdvanced:
            default:
                throw new Exception($"Invalid search modifier for string: {sp.ModifierLiteral}");
        }
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
                case ParsedSearchParameter.SearchModifierCodes.None:
                    {
                        if (sp.Values.Any(v => value.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    break;

                case ParsedSearchParameter.SearchModifierCodes.Contains:
                    {
                        if (sp.Values.Any(v => value.Contains(v, StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    break;

                case ParsedSearchParameter.SearchModifierCodes.Exact:
                    {
                        if (sp.Values.Any(v => value.Equals(v, StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                    break;

                case ParsedSearchParameter.SearchModifierCodes.Missing:
                    {
                        if (sp.Values.Any(v =>
                            (v.StartsWith("t", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(value)) ||
                            (v.StartsWith("f", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(value))))
                        {
                            return true;
                        }
                    }
                    break;

                case ParsedSearchParameter.SearchModifierCodes.Not:
                    {
                        if (sp.Values.Any(v => !value.Equals(v, StringComparison.Ordinal)))
                        {
                            return true;
                        }
                    }
                    break;

                case ParsedSearchParameter.SearchModifierCodes.ResourceType:
                case ParsedSearchParameter.SearchModifierCodes.Above:
                case ParsedSearchParameter.SearchModifierCodes.Below:
                case ParsedSearchParameter.SearchModifierCodes.CodeText:
                case ParsedSearchParameter.SearchModifierCodes.Identifier:
                case ParsedSearchParameter.SearchModifierCodes.In:
                case ParsedSearchParameter.SearchModifierCodes.Iterate:
                case ParsedSearchParameter.SearchModifierCodes.NotIn:
                case ParsedSearchParameter.SearchModifierCodes.OfType:
                case ParsedSearchParameter.SearchModifierCodes.Text:
                case ParsedSearchParameter.SearchModifierCodes.TextAdvanced:
                default:
                    throw new Exception($"Invalid search modifier for HumanName: {sp.ModifierLiteral}");
            }
        }

        return false;
    }
}
