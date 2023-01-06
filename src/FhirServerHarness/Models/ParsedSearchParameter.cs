// <copyright file="ParsedSearchParameter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Extensions;
using FhirServerHarness.Storage;
using Hl7.Fhir.Utility;
using System.Collections.Immutable;

namespace FhirServerHarness.Models;

/// <summary>A parsed search parameter.</summary>
public class ParsedSearchParameter
{
    /// <summary>Values that represent search parameter type codes.</summary>
    public enum SearchParameterTypeCodes
    {
        /// <summary>
        /// A date parameter searches on a date/time or period.
        /// </summary>
        Date,

        /// <summary>
        /// Searching on a simple numerical value in a resource.
        /// </summary>
        Number,

        /// <summary>
        /// A quantity parameter searches on the Quantity datatype.
        /// </summary>
        Quantity,

        /// <summary>
        /// A reference parameter refers to references between resources.
        /// </summary>
        Reference,

        /// <summary>
        /// For a simple string search, a string parameter serves as the input 
        /// for a search against sequences of characters.
        /// </summary>
        String,

        /// <summary>
        /// A token type is a parameter that provides a close to exact match 
        /// search on a string of characters, potentially scoped by a URI.
        /// </summary>
        Token,

        /// <summary>
        /// The uri parameter refers to an element that contains a URI (RFC 3986).
        /// </summary>
        Uri,

        /// <summary>
        /// Composite search parameters are allow joining multiple elements into distinct single values with a $.
        /// </summary>
        Composite,

        /// <summary>
        /// The way this parameter works is unique to the parameter and described with the parameter.
        /// </summary>
        Special,
    }

    /// <summary>Values that represent search modifier codes.</summary>
    public enum SearchModifierCodes
    {
        /// <summary>An enum constant representing the none option.</summary>
        None,
        
        /// <summary>
        /// Tests whether the value in a resource points to a resource of the supplied 
        /// parameter type. Note: a concrete ResourceType is specified as the modifier 
        /// (e.g., not the literal :[type], but a value such as :Patient).
        /// </summary>
        ResourceType,

        /// <summary>
        /// Tests whether the value in a resource is or subsumes the supplied parameter value 
        /// (is-a, or hierarchical relationships).
        /// </summary>
        [FhirLiteral("above")]
        Above,

        /// <summary>
        /// Tests whether the value in a resource is or is subsumed by the supplied parameter 
        /// value (is-a, or hierarchical relationships).
        /// </summary>
        [FhirLiteral("below")]
        Below,

        /// <summary>
        /// Tests whether the textual display value in a resource (e.g., CodeableConcept.text, 
        /// Coding.display, or Reference.display) matches the supplied parameter value.
        /// </summary>
        [FhirLiteral("code-text")]
        CodeText,

        /// <summary>
        /// Tests whether the value in a resource includes the supplied parameter value 
        /// anywhere within the field being searched.
        /// </summary>
        [FhirLiteral("contains")]
        Contains,

        /// <summary>
        /// Tests whether the value in a resource exactly matches the supplied parameter 
        /// value (the whole string, including casing and accents).
        /// </summary>
        [FhirLiteral("exact")]
        Exact,

        /// <summary>
        /// Tests whether the Reference.identifier in a resource (rather than the 
        /// Reference.reference) matches the supplied parameter value.
        /// </summary>
        [FhirLiteral("identifier")]
        Identifier,

        /// <summary>
        /// Tests whether the value in a resource is a member of the supplied parameter ValueSet.
        /// </summary>
        [FhirLiteral("in")]
        In,

        /// <summary>
        /// The search parameter indicates an inclusion directive (_include, _revinclude) that 
        /// is applied to an included resource instead of the matching resource.
        /// </summary>
        [FhirLiteral("iterate")]
        Iterate,

        /// <summary>
        /// Tests whether the value in a resource is present (when the supplied parameter 
        /// value is true) or absent (when the supplied parameter value is false).
        /// </summary>
        [FhirLiteral("missing")]
        Missing,

        /// <summary>
        /// Tests whether the value in a resource does not match the specified parameter 
        /// value. Note that this includes resources that have no value for the parameter.
        /// </summary>
        [FhirLiteral("not")]
        Not,

        /// <summary>
        /// Tests whether the value in a resource is not a member of the supplied parameter ValueSet.
        /// </summary>
        [FhirLiteral("not-in")]
        NotIn,

        /// <summary>
        /// Tests whether the Identifier value in a resource matches the supplied parameter value.
        /// </summary>
        [FhirLiteral("of-type")]
        OfType,

        /// <summary>
        /// reference, token:
        ///     Tests whether the textual value in a resource (e.g., CodeableConcept.text, 
        ///     Coding.display, Identifier.type.text, or Reference.display) matches the supplied 
        ///     parameter value using basic string matching (begins with or is, case-insensitive).
        /// text:
        ///     The search parameter value should be processed as input to a search with advanced text handling.
        /// </summary>
        [FhirLiteral("text")]
        Text,

        /// <summary>
        /// Tests whether the value in a resource matches the supplied parameter value using advanced text 
        /// handling that searches text associated with the code/value - e.g., CodeableConcept.text, 
        /// Coding.display, or Identifier.type.text.
        /// </summary>
        [FhirLiteral("text-advanced")]
        TextAdvanced,
    }

    /// <summary>Values that represent search prefix codes.</summary>
    public enum SearchPrefixCodes
    {
        /// <summary>
        /// The value for the parameter in the resource is equal to the provided value.
        /// </summary>
        [FhirLiteral("eq")]
        Equal,

        /// <summary>
        /// The value for the parameter in the resource exists and is not equal to the provided value.
        /// </summary>
        [FhirLiteral("ne")]
        NotEqual,

        /// <summary>
        /// The value for the parameter in the resource exists and is greater than the provided value.
        /// </summary>
        [FhirLiteral("gt")]
        GreaterThan,

        /// <summary>
        /// The value for the parameter in the resource exists and is less than the provided value.
        /// </summary>
        [FhirLiteral("lt")]
        LessThan,

        /// <summary>
        /// The value for the parameter in the resource exists and is greater or equal to the provided value.
        /// </summary>
        [FhirLiteral("ge")]
        GreaterThanOrEqual,

        /// <summary>
        /// The value for the parameter in the resource exists and is less or equal to the provided value.
        /// </summary>
        [FhirLiteral("le")]
        LessThanOrEqual,

        /// <summary>
        /// The value for the parameter in the resource exists and starts after the provided value.
        /// </summary>
        [FhirLiteral("sa")]
        StartsAfter,

        /// <summary>
        /// The value for the parameter in the resource exists and ends before the provided value.
        /// </summary>
        [FhirLiteral("eb")]
        EndsBefore,

        /// <summary>
        /// The value for the parameter in the resource exists and is approximately the same to the provided value.
        /// </summary>
        [FhirLiteral("ap")]
        Approximately,
    }

    /// <summary>Values that represent parameter type codes.</summary>
    public enum ParameterTypeCodes
    {
        /// <summary>An enum constant representing the HTTP parameter option.</summary>
        HttpParameter,

        /// <summary>An enum constant representing the search result parameter option.</summary>
        SearchResultParameter,

        /// <summary>An enum constant representing the all resource parameter option.</summary>
        AllResourceParameter,

        /// <summary>An enum constant representing the resource-specific parameter option.</summary>
        ResourceParameter,

        /// <summary>An enum constant representing the subscription topic parameter option.</summary>
        SubscriptionTopicParameter,
    }

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
    internal static readonly Dictionary<string, string> _allResourceParameters = new()
    {
        /// <summary>Searching all textual content of a resource.</summary>
        {"_content", "" },

        /// <summary>Specify an arbitrary query via filter syntax.</summary>
        { "_filter", "" },

        /// <summary>Searching based on the logical identifier of resources (Resource.id).</summary>
        { "_id", "Resource.id" },

        /// <summary>Match resources against active membership in collection resources.</summary>
        { "_in", "" },

        /// <summary>Match resources based on the language of the resource used (Resource.language).</summary>
        { "_language", "Resource.language" },

        /// <summary>Match resources based on when the most recent change has been made (Resource.meta.lastUpdated).</summary>
        { "_lastUpdated", "Resource.meta.lastUpdated" },

        /// <summary>Test resources against references in a List resource.</summary>
        { "_list", "" },

        /// <summary>Match resources based on values in the Resource.meta.profile element.</summary>
        { "_profile", "Resource.meta.profile" },

        /// <summary>Execute a pre-defined and named query operation.</summary>
        { "_query", "" },

        /// <summary>Match resources based on security labels in the Resource.meta.security.</summary>
        { "_security", "Resource.meta.security" },

        /// <summary>Match resources based on tag information in the Resource.meta.tag element.</summary>
        { "_tag", "Resource.meta.tag" },

        /// <summary>Perform searches against the narrative content of a resource.</summary>
        { "_text", "" },

        /// <summary>Allow filtering of types in searches that are performed across multiple resource types (e.g., searches across the server root).</summary>
        { "_type", "" },
    };

    /// <summary>Gets or sets the name.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the value.</summary>
    public required string Value { get; set; }

    /// <summary>Gets or sets the type of the parameter.</summary>
    public required SearchParameterTypeCodes ParamType { get; set; }

    /// <summary>Gets or sets the modifier.</summary>
    public string? ModifierLiteral { get; set; } = null;

    /// <summary>Gets or sets the modifier.</summary>
    public SearchModifierCodes Modifier { get; set; } = SearchModifierCodes.None;

    /// <summary>Gets or sets the prefix.</summary>
    public string? Prefix { get; set; } = null;

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

            string[] keyComponents = key.Split(':');

            string sp = keyComponents[0];

            if (sp.Contains('.'))
            {
                // TODO: handle chaining
            }

            // TODO: handle reverse chaining (additional ':')

            // TODO: check for modifiers (SearchModifierCodes)
            // TODO: check for resourceType modifier (need to match resources in the store)



            // TODO: Remove WIP
            yield return new ParsedSearchParameter
            {
                Name = sp,
                ParamType = SearchParameterTypeCodes.String,
                ModifierLiteral = keyComponents.Length > 1 ? keyComponents[1] : null,
                Prefix = keyComponents.Length > 2 ? keyComponents[2] : null,
                SelectExpression = "Resource.id",
                Value = value,
            };


            //var parameter = Parse(key, value);
            //if (parameter != null)
            //{
            //    yield return parameter;
            //}
        }
    }
}