// <copyright file="ParsedSearchParameter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Extensions;
using FhirServerHarness.Storage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using System.Collections.Immutable;
using static FhirServerHarness.Search.SearchDefinitions;

namespace FhirServerHarness.Models;

/// <summary>A parsed search parameter.</summary>
public class ParsedSearchParameter
{

    /// <summary>(Immutable) Options for controlling the HTTP.</summary>
    internal static readonly ImmutableHashSet<string> _httpParameters = ImmutableHashSet.Create(new string[]
    {
        /// <summary>Override the HTTP content negotiation.</summary>
        "_format",

        /// <summary>Ask for a pretty printed response for human convenience.</summary>
        "_pretty",

        /// <summary>Ask for a predefined short form of the resource in response.</summary>
        "_summary",

        /// <summary>Ask for a particular set of elements to be returned.</summary>
        "_elements",
    });

    /// <summary>(Immutable) Options for controlling the search result.</summary>
    internal static readonly ImmutableHashSet<string> _searchResultParameters = ImmutableHashSet.Create(new string[]
    {
        /// <summary>Request different types of handling for contained resources.</summary>
        "_contained",

        /// <summary>Limit the number of match results per page of response..</summary>
        "_count",

        /// <summary>Include additional resources according to a GraphDefinition.</summary>
        "_graph",

        /// <summary>Include additional resources, based on following links forward across references..</summary>
        "_include",

        /// <summary>Include additional resources, based on following reverse links across references.</summary>
        "_revinclude",

        /// <summary>Request match relevance in results.</summary>
        "_score",

        /// <summary>Request which order results should be returned in.</summary>
        "_sort",

        /// <summary>Request a precision of the total number of results for a request.</summary>
        "_total"
    });

    /// <summary>(Immutable) Options for controlling all resource.</summary>
    internal static readonly Dictionary<string, ModelInfo.SearchParamDefinition> _allResourceParameters = new()
    {
        /// <summary>Searching all textual content of a resource.</summary>
        {"_content", new() { Name = "_content", Type = SearchParamType.Special } },

        /// <summary>Specify an arbitrary query via filter syntax.</summary>
        { "_filter", new() { Name = "_filter", Type = SearchParamType.Special } },

        /// <summary>Searching based on the logical identifier of resources (Resource.id).</summary>
        { "_id", new() { Name = "_id", Expression = "Resource.id", Type = SearchParamType.Token } },

        /// <summary>Match resources against active membership in collection resources.</summary>
        { "_in", new() { Name = "_in", Type = SearchParamType.Reference } },

        /// <summary>Match resources based on the language of the resource used (Resource.language).</summary>
        { "_language", new() { Name = "_language", Expression = "Resource.language", Type = SearchParamType.Token } },

        /// <summary>Match resources based on when the most recent change has been made (Resource.meta.lastUpdated).</summary>
        { "_lastUpdated", new() { Name = "_lastUpdated", Expression = "Resource.meta.lastUpdated", Type = SearchParamType.Date } },

        /// <summary>Test resources against references in a List resource.</summary>
        { "_list", new() { Name = "_list", Type = SearchParamType.Special } },

        /// <summary>Match resources based on values in the Resource.meta.profile element.</summary>
        { "_profile", new() { Name = "_profile", Expression = "Resource.meta.profile", Type = SearchParamType.Reference } },

        /// <summary>Execute a pre-defined and named query operation.</summary>
        { "_query", new() { Name = "_query", Type = SearchParamType.Token } },

        /// <summary>Match resources based on security labels in the Resource.meta.security.</summary>
        { "_security", new() { Name = "_security", Expression = "Resource.meta.security", Type = SearchParamType.Token } },
        
        /// <summary>Match resources based on tag information in the Resource.meta.source element</summary>
        { "_source", new() { Name = "_security", Expression = "Resource.meta.source", Type = SearchParamType.Uri } },

        /// <summary>Match resources based on tag information in the Resource.meta.tag element.</summary>
        { "_tag", new() { Name ="_tag", Expression = "Resource.meta.tag", Type = SearchParamType.Token } },

        /// <summary>Perform searches against the narrative content of a resource.</summary>
        { "_text", new() { Name = "_text", Type = SearchParamType.String } },

        /// <summary>Allow filtering of types in searches that are performed across multiple resource types (e.g., searches across the server root).</summary>
        { "_type", new() { Name = "_type", Type = SearchParamType.Token } },
    };

    /// <summary>Gets or sets the name.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the values.</summary>
    public required string[] Values { get; set; }

    /// <summary>Gets or sets the prefix.</summary>
    public SearchPrefixCodes?[] Prefixes { get; set; } = Array.Empty<SearchPrefixCodes?>();

    /// <summary>Gets or sets the type of the parameter.</summary>
    public required SearchParamType ParamType { get; set; }

    /// <summary>Gets or sets the modifier.</summary>
    public string? ModifierLiteral { get; set; } = null;

    /// <summary>Gets or sets the modifier.</summary>
    public SearchModifierCodes Modifier { get; set; } = SearchModifierCodes.None;

    /// <summary>Gets or sets the fhirPath extraction query.</summary>
    public required string SelectExpression { get; set; }

    /// <summary>Enumerates parse in this collection.</summary>
    /// <param name="queryString">  The query string.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="store">        The store.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process parse in this collection.
    /// </returns>
    public static IEnumerable<ParsedSearchParameter> Parse(
        string queryString,
        IResourceStore resourceStore,
        IFhirStore store)
    {
        if (string.IsNullOrWhiteSpace(queryString))
        {
            yield break;
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

            if (key.Contains('.'))
            {
                // TODO: handle chaining
            }

            string[] keyComponents = key.Split(':');

            string sp = keyComponents[0];

            if (keyComponents.Length > 2)
            {
                if (!keyComponents.Any(kc => kc.Equals("_has")))
                {
                    // TODO: need to fail query, not throw
                    throw new Exception($"too many modifiers: {key}");
                }

                // TODO: handle reverse chaining (_has contains additional ':')
            }

            // check for modifiers (SearchModifierCodes)
            string modifierLiteral = string.Empty;
            SearchModifierCodes modifierCode = SearchModifierCodes.None;

            if (keyComponents.Length == 2)
            {
                modifierLiteral = keyComponents[1];
                if (!Enum.TryParse(modifierLiteral, true, out modifierCode))
                {
                    // TODO: need to fail query, not throw
                    throw new Exception($"unknown modifier: {modifierLiteral}");
                }
            }

            // TODO: check for resourceType modifier (need to match resources in the store)


            ModelInfo.SearchParamDefinition? spd = null;

            if (_allResourceParameters.ContainsKey(sp))
            {
                spd = _allResourceParameters[sp];
            }

            if ((spd == null) &&
                (resourceStore != null))
            {
                if (!resourceStore.TryGetSearchParamDefinition(sp, out spd))
                {
                    // no definition found
                    Console.WriteLine($"Unknown search parameter: {sp}");
                    continue;
                }
            }

            List<SearchPrefixCodes?> prefixes = new();
            List<string> values = new();

            // parse parameter string, looking for multi-value
            int index = 0;
            while (index < value.Length)
            {
                int nextIndex = value.IndexOf(',', index);
                if (nextIndex == -1)
                {
                    // unescape commas
                    values.Add(value.Substring(index).Replace("\\,", ","));
                    break;
                }

                // check for no content (e.g., ",,")
                if (nextIndex == (index + 1))
                {
                    // ignore this value and continue
                    continue;
                }

                // check to see if this comma is escaped
                if (value[nextIndex - 1] == '\\')
                {
                    // do not move the start, keep looking for the next comma
                    continue;
                }

                // unescape any escaped commas and add this value
                values.Add(value.Substring(index, nextIndex - index).Replace("\\,", ","));
                index = nextIndex + 1;
            }

            switch (spd!.Type)
            {
                // parameter types that allow prefixes
                case SearchParamType.Number:
                case SearchParamType.Date:
                case SearchParamType.Quantity:
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i].Length < 2)
                        {
                            continue;
                        }

                        if (values[i].Substring(0, 2).TryFhirEnum(out SearchPrefixCodes prefix))
                        {
                            prefixes.Add(prefix);
                            values[i] = values[i].Substring(2);
                        }
                        else
                        {
                            prefixes.Add(null);
                        }
                    }

                    break;
                //case SearchParamType.String:
                //case SearchParamType.Token:
                //case SearchParamType.Reference:
                //case SearchParamType.Composite:
                //case SearchParamType.Uri:
                //case SearchParamType.Special:
                default:
                    break;
            }

            yield return new ParsedSearchParameter
            {
                Name = sp,
                ParamType = spd.Type,
                ModifierLiteral = modifierLiteral,
                Modifier = modifierCode,
                Prefixes = prefixes.ToArray(),
                SelectExpression = spd.Expression ?? string.Empty,
                Values = values.ToArray(),
            };

            continue;

            //// TODO: Remove WIP
            //yield return new ParsedSearchParameter
            //{
            //    Name = sp,
            //    ParamType = SearchParamType.String,
            //    ModifierLiteral = keyComponents.Length > 1 ? keyComponents[1] : null,
            //    Prefix = keyComponents.Length > 2 ? keyComponents[2] : null,
            //    SelectExpression = "Resource.id",
            //    Value = value,
            //};


            //var parameter = Parse(key, value);
            //if (parameter != null)
            //{
            //    yield return parameter;
            //}
        }
    }
}