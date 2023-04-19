﻿// <copyright file="ParsedResultParameter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Extensions;
using FhirServerHarness.Storage;
using Hl7.Fhir.Model;

namespace FhirServerHarness.Models;

/// <summary>A parsed search result parameter.</summary>
public class ParsedResultParameters
{
    /// <summary>(Immutable) Options for controlling the search result.</summary>
    public static readonly HashSet<string> SearchResultParameters = new()
    {
        "_contained",
        "_count",
        "_elements",
        "_graph",
        "_include",
        "_include:iterate",
        "_maxresults",
        "_revinclude",
        "_score",
        "_sort",
        "_summary",
        "_total",
    };

    /// <summary>Gets or sets the inclusion FHIRpath extractions, keyed by resource.</summary>
    public Dictionary<string, List<ModelInfo.SearchParamDefinition>> Inclusions { get; set; } = new();

    /// <summary>Gets or sets the iterative inclusion FHIRpath extractions, keyed by resource.</summary>
    public Dictionary<string, List<string>> IterativeInclusions { get; set; } = new();

    /// <summary>Gets or sets the reverse inclusion search parameter definitions, keyed by resource.</summary>
    public Dictionary<string, List<ModelInfo.SearchParamDefinition>> ReverseInclusions { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the FhirServerHarness.Models.ParsedResultParameters class.
    /// </summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="store">      The FHIR store.</param>
    public ParsedResultParameters(string queryString, IFhirStore store)
    {
        Parse(queryString, store);
    }

    /// <summary>Enumerates parse in this collection.</summary>
    /// <param name="queryString">The query string.</param>
    /// <param name="store">      The FHIR store.</param>
    private void Parse(string queryString, IFhirStore store)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            return;
        }

        System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(queryString);
        foreach (string key in query)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            string? value = query[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (key)
            {
                case "_contained":
                    break;

                case "_count":
                    break;

                case "_elements":
                    break;

                case "_graph":
                    break;

                case "_include":
                    {
                        string[] components = value.Split(':');

                        ResourceType? rt = null;

                        switch (components.Length)
                        {
                            // _include=[resource]:[parameter]
                            case 2:
                                break;

                            // _include=[resource]:[parameter]:[targetType]
                            case 3:
                                rt = ModelInfo.FhirTypeNameToResourceType(components[2]);
                                break;

                            // invalid / unknown
                            default:
                                continue;
                        }

                        if ((!store.TryGetSearchParamDefinition(components[0], components[1], out ModelInfo.SearchParamDefinition? spDefinition)) ||
                            (spDefinition == null))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(spDefinition.Expression))
                        {
                            continue;
                        }

                        if (!Inclusions.ContainsKey(components[0]))
                        {
                            Inclusions.Add(components[0], new());
                        }

                        // if we have a third component, it's a resource type
                        if (rt != null)
                        {
                            // override the default allowed targets to only the one specified
                            spDefinition = spDefinition.CloneWith(new ResourceType[] { (ResourceType)rt });
                        }

                        Inclusions[components[0]].Add(spDefinition);
                    }
                    break;

                case "_include:iterate":
                    {
                        string[] components = value.Split(':');

                        if (components.Length != 2)
                        {
                            continue;
                        }

                        if ((!store.TryGetSearchParamDefinition(components[0], components[1], out ModelInfo.SearchParamDefinition? spDefinition)) ||
                            (spDefinition == null))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(spDefinition.Expression))
                        {
                            continue;
                        }

                        if (!IterativeInclusions.ContainsKey(components[0]))
                        {
                            IterativeInclusions.Add(components[0], new());
                        }

                        IterativeInclusions[components[0]].Add(spDefinition.Expression);
                    }
                    break;

                case "_maxresults":
                    break;

                case "_revinclude":
                    {
                        string[] components = value.Split(':');

                        ResourceType? rt = null;

                        switch (components.Length)
                        {
                            // _revinclude=[resource]:[parameter]
                            case 2:
                                break;

                            // _revinclude=[resource]:[parameter]:[targetType]
                            case 3:
                                rt = ModelInfo.FhirTypeNameToResourceType(components[2]);
                                break;

                            // invalid / unknown
                            default:
                                continue;
                        }

                        if ((!store.TryGetSearchParamDefinition(components[0], components[1], out ModelInfo.SearchParamDefinition? spDefinition)) ||
                            (spDefinition == null))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(spDefinition.Expression))
                        {
                            continue;
                        }

                        // if we have a third component, it's a resource type
                        if (rt != null)
                        {
                            // override the default allowed targets to only the one specified
                            spDefinition = spDefinition.CloneWith(new ResourceType[] { (ResourceType)rt });
                        }

                        if (!ReverseInclusions.ContainsKey(components[0]))
                        {
                            ReverseInclusions.Add(components[0], new());
                        }

                        ReverseInclusions[components[0]].Add(spDefinition);
                    }
                    break;

                case "_score":
                    break;

                case "_sort":
                    break;

                case "_summary":
                    break;

                case "_total":
                    break;
            }
        }
    }

}