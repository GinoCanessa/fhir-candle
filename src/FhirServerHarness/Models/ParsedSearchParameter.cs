// <copyright file="ParsedSearchParameter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Extensions;
using FhirServerHarness.Storage;
using Hl7.Fhir.Model;
using Hl7.Fhir.Utility;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
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

    /// <summary>Gets or sets the date starts.</summary>
    public DateTimeOffset[]? ValueDateStarts { get; set; }

    /// <summary>Gets or sets the date ends.</summary>
    public DateTimeOffset[]? ValueDateEnds { get; set; }

    /// <summary>Gets or sets the values for integer types.</summary>
    public long[]? ValueInts { get; set; }

    /// <summary>Gets or sets the values for decimal types.</summary>
    public decimal[]? ValueDecimals { get; set; }

    /// <summary>Gets or sets the value FHIR codes.</summary>
    public Hl7.Fhir.ElementModel.Types.Code[]? ValueFhirCodes { get; set; }

    /// <summary>Gets or sets the value bools.</summary>
    public bool[]? ValueBools { get; set; }

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

    /// <summary>
    /// Initializes a new instance of the FhirServerHarness.Models.ParsedSearchParameter class.
    /// </summary>
    /// <param name="name">           The name.</param>
    /// <param name="modifierLiteral">The modifier.</param>
    /// <param name="modifierCode">   The modifier code.</param>
    /// <param name="value">          The value.</param>
    /// <param name="spd">            The speed.</param>
    [SetsRequiredMembers]
    public ParsedSearchParameter(
        string name, 
        string modifierLiteral,
        SearchModifierCodes modifierCode,
        string value,
        ModelInfo.SearchParamDefinition spd)
    {
        Name = name;
        Modifier = modifierCode;
        ModifierLiteral = modifierLiteral;
        ParamType = spd.Type;
        SelectExpression = spd.Expression ?? string.Empty;

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

        // check for prefixes
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

        Prefixes = prefixes.ToArray();
        Values = values.ToArray();

        // parse value types that require additional conversion
        switch (spd!.Type)
        {
            case SearchParamType.Date:
                {
                    ValueDateStarts = new DateTimeOffset[values.Count];
                    ValueDateEnds = new DateTimeOffset[values.Count];

                    for (int i = 0; i < values.Count; i++)
                    {
                        if (TryParseDateString(values[i], out DateTimeOffset start, out DateTimeOffset end))
                        {
                            ValueDateStarts[i] = start;
                            ValueDateEnds[i] = end;
                        }
                    }
                }
                break;

            case SearchParamType.Number:
                {
                    // check for input decimal types
                    if (value.Contains('.') || value.Contains("e-", StringComparison.OrdinalIgnoreCase))
                    {
                        // use decimal
                        ValueDecimals = new decimal[values.Count];

                        for (int i = 0; i < values.Count; i++)
                        {
                            if (decimal.TryParse(values[i], out decimal val))
                            {
                                ValueDecimals[i] = val;
                            }
                        }
                    }
                    else
                    {
                        // use longs
                        ValueInts = new long[values.Count];

                        for (int i = 0; i < values.Count; i++)
                        {
                            if (long.TryParse(values[i], out long val))
                            {
                                ValueInts[i] = val;
                            }
                        }
                    }
                }
                break;

            case SearchParamType.Quantity:
                {
                    // use decimal for values
                    ValueDecimals = new decimal[values.Count];
                    ValueFhirCodes = new Hl7.Fhir.ElementModel.Types.Code[values.Count];

                    // traverse values
                    for (int i = 0; i < values.Count; i++)
                    {
                        string[] components = values[i].Split('|', StringSplitOptions.RemoveEmptyEntries);

                        // value is always first
                        if (decimal.TryParse(components[0], out decimal val))
                        {
                            ValueDecimals[i] = val;
                        }

                        switch (components.Length)
                        {
                            // value and code / unit
                            case 2:
                                ValueFhirCodes[i] = new(null, components[1]);
                                break;

                            // value, system, and code / unit
                            case 3:
                                ValueFhirCodes[i] = new(components[1], components[2]);
                                break;
                        }
                    }
                }
                break;

            case SearchParamType.Token:
                {
                    // check for boolean tokens
                    if (values.All(v => v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase)))
                    {
                        ValueBools = new bool[values.Count];

                        // traverse values
                        for (int i = 0; i < values.Count; i++)
                        {
                            _ = bool.TryParse(values[i], out ValueBools[i]);
                        }
                    }

                    // tokens always represent a code and system
                    ValueFhirCodes = new Hl7.Fhir.ElementModel.Types.Code[values.Count];

                    // traverse values
                    for (int i = 0; i < values.Count; i++)
                    {
                        string[] components = values[i].Split('|');

                        if (components.Length == 1)
                        {
                            ValueFhirCodes[i] = new(null, components[0]);
                        }
                        else
                        {
                            ValueFhirCodes[i] = new(components[0], components[1]);
                        }
                    }
                }
                break;

                //case SearchParamType.String:
                //case SearchParamType.Reference:
                //case SearchParamType.Composite:
                //case SearchParamType.Uri:
                //case SearchParamType.Special:
        }
    }

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

            if (string.IsNullOrEmpty(spd?.Expression))
            {
                throw new Exception($"Cannot process parameter without an expression: {spd?.Name}");
            }

            yield return new ParsedSearchParameter(
                sp,
                modifierLiteral,
                modifierCode,
                value,
                spd);

            //yield return new ParsedSearchParameter
            //{
            //    Name = sp,
            //    ParamType = spd.Type,
            //    ModifierLiteral = modifierLiteral,
            //    Modifier = modifierCode,
            //    Prefixes = prefixes.ToArray(),
            //    SelectExpression = spd.Expression ?? string.Empty,
            //    Values = values.ToArray(),
            //};

            continue;
        }
    }

    /// <summary>Attempts to parse a date string.</summary>
    /// <param name="dateString">The date string.</param>
    /// <param name="start">     [out] The start.</param>
    /// <param name="end">       [out] The end.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public static bool TryParseDateString(string dateString, out DateTimeOffset start, out DateTimeOffset end)
    {
        // note that we are using DateTime and converting to DateTimeOffset to work through TZ stuff without manually parsing each format precision
        if (!DateTime.TryParse(dateString, null, DateTimeStyles.RoundtripKind, out DateTime dt))
        {
            Console.WriteLine($"Failed to parse date: {dateString}");
            start = DateTimeOffset.MinValue;
            end = DateTimeOffset.MaxValue;
            return false;
        }

        start = new DateTimeOffset(dt, TimeSpan.Zero);

        switch (dateString.Length)
        {
            // YYYY
            case 4:
                end = start.AddYears(1).AddTicks(-1);
                break;

            // YYYY-MM
            case 7:
                end = start.AddMonths(1).AddTicks(-1);
                break;

            // YYYY-MM-DD
            case 10:
                end = start.AddDays(1).AddTicks(-1);
                break;

            // Note: this is not defined as valid, but wanted to support it
            // YYYY-MM-DDThh
            case 13:
                end = start.AddHours(1).AddTicks(-1);
                break;

            // YYYY-MM-DDThh:mm
            case 16:
                end = start.AddMinutes(1).AddTicks(-1);
                break;

            // Note: servers are allowed to ignore fractional seconds - I am chosing to do so.

            // YYYY-MM-DDThh:mm:ss
            case 19:
            // YYYY-MM-DDThh:mm:ssZ
            case 20:
            // YYYY-MM-DDThh:mm:ss+zz
            // YYYY-MM-DDThh:mm:ss.fZ
            case 22:
            // YYYY-MM-DDThh:mm:ss.ffZ
            case 23:
            // YYYY-MM-DDThh:mm:ss.fffZ
            case 24:
            // YYYY-MM-DDThh:mm:ss+zz:zz
            // YYYY-MM-DDThh:mm:ss.ffffZ
            case 25:
            // YYYY-MM-DDThh:mm:ss.f+zz:zz
            case 27:
            // YYYY-MM-DDThh:mm:ss.ff+zz:zz
            case 28:
            // YYYY-MM-DDThh:mm:ss.fff+zz:zz
            case 29:
            // YYYY-MM-DDThh:mm:ss.ffff+zz:zz
            case 30:
                end = start.AddSeconds(1).AddTicks(-1);
                break;

            default:
                Console.WriteLine($"Invalid date format: {dateString}");
                start = DateTimeOffset.MinValue;
                end = DateTimeOffset.MaxValue;
                return false;
        }

        return true;
    }
}