// <copyright file="SearchDefinitions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Extensions;
using Hl7.Fhir.Model;

namespace FhirServerHarness.Search;

/// <summary>Common search definitions.</summary>
public static class SearchDefinitions
{
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

    /// <summary>(Immutable) Only missing modifier, for types that use prefixes.</summary>
    private static readonly HashSet<SearchModifierCodes> _onlyMissing = new()
    {
        SearchModifierCodes.Missing,
    };

    /// <summary>(Immutable) The modifiers for date.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForDate = _onlyMissing;

    /// <summary>(Immutable) The modifiers for number.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForNumber = _onlyMissing;

    /// <summary>(Immutable) The modifiers for quantity.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForQuantity = _onlyMissing;

    /// <summary>(Immutable) The modifiers for reference.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForReference = new()
    {
        SearchModifierCodes.Above,
        SearchModifierCodes.Below,
        SearchModifierCodes.CodeText,
        SearchModifierCodes.Identifier,
        SearchModifierCodes.In,
        SearchModifierCodes.Missing,
        SearchModifierCodes.NotIn,
        SearchModifierCodes.Text,
        SearchModifierCodes.TextAdvanced,
        SearchModifierCodes.ResourceType,
    };

    /// <summary>(Immutable) The modifiers for string.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForString = new()
    {
        SearchModifierCodes.Contains,
        SearchModifierCodes.Exact,
        SearchModifierCodes.Missing,
        SearchModifierCodes.Text,
    };

    /// <summary>(Immutable) The modifiers for token.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForToken = new()
    {
        SearchModifierCodes.Above,
        SearchModifierCodes.Below,
        SearchModifierCodes.CodeText,
        SearchModifierCodes.In,
        SearchModifierCodes.Missing,
        SearchModifierCodes.Not,
        SearchModifierCodes.NotIn,
        SearchModifierCodes.OfType,
        SearchModifierCodes.Text,
        SearchModifierCodes.TextAdvanced,
    };

    /// <summary>(Immutable) URI of the modifiers for.</summary>
    public static readonly HashSet<SearchModifierCodes> ModifiersForUri = new()
    {
        SearchModifierCodes.Above,
        SearchModifierCodes.Below,
        SearchModifierCodes.Contains,
        SearchModifierCodes.In,
        SearchModifierCodes.Missing,
        SearchModifierCodes.Not,
        SearchModifierCodes.NotIn,
        SearchModifierCodes.OfType,
        SearchModifierCodes.Text,
        SearchModifierCodes.TextAdvanced,
    };

    /// <summary>(Immutable) The supported search combinations.</summary>
    public static readonly HashSet<string> SupportedSearchCombinations = new()
    {
        "date-date",
        "date-datetime",
        "date-instant",
        "date-period",
        "date-timing",
        "number-integer",
        "number-unsignedint",
        "number-positiveint",
        "number-integer64",
        "number-decimal",
        "quantity-quantity",
        "reference-uri",
        "reference-url",
        "reference-canonical",
        "reference-reference",
        "reference-oid",
        "reference-uuid",
        "string-id",
        "string-string",
        "string-markdown",
        "string-xhtml",
        "string-contains-id",
        "string-contains-string",
        "string-contains-markdown",
        "string-contains-xhtml",
        "string-exact-id",
        "string-exact-string",
        "string-exact-markdown",
        "string-exact-xhtml",
        "string-humanname",
        "string-contains-humanname",
        "string-exact-humanname",
        "token-canonical",
        "token-id",
        "token-oid",
        "token-uri",
        "token-url",
        "token-uuid",
        "token-string",
        "token-not-canonical",
        "token-not-id",
        "token-not-oid",
        "token-not-uri",
        "token-not-url",
        "token-not-uuid",
        "token-not-string",
        "token-boolean",
        "token-not-boolean",
        "token-code",
        "token-coding",
        "token-contactpoint",
        "token-identifier",
        "token-not-code",
        "token-not-coding",
        "token-not-contactpoint",
        "token-not-identifier",
        "token-codeableconcept",
        "uri-canonical",
        "uri-uri",
        "uri-url",
        "uri-oid",
        "uri-uuid",
    };

    /// <summary>(Immutable) The unsupported search combinations.</summary>
    public static readonly HashSet<string> UnsupportedSearchCombinations = new()
    {
        "reference-above-canonical",
        "reference-above-reference",
        "reference-above-oid",
        "reference-above-uri",
        "reference-above-url",
        "reference-above-uuid",
        "reference-below-canonical",
        "reference-below-reference",
        "reference-below-oid",
        "reference-below-uri",
        "reference-below-url",
        "reference-below-uuid",
        "reference-code-text-canonical",
        "reference-code-text-reference",
        "reference-code-text-oid",
        "reference-code-text-uri",
        "reference-code-text-url",
        "reference-code-text-uuid",
        "reference-identifier-canonical",
        "reference-identifier-reference",
        "reference-identifier-oid",
        "reference-identifier-uri",
        "reference-identifier-url",
        "reference-identifier-uuid",
        "reference-in-canonical",
        "reference-in-reference",
        "reference-in-oid",
        "reference-in-uri",
        "reference-in-url",
        "reference-in-uuid",
        "reference-not-in-canonical",
        "reference-not-in-reference",
        "reference-not-in-oid",
        "reference-not-in-uri",
        "reference-not-in-url",
        "reference-not-in-uuid",
        "reference-text-canonical",
        "reference-text-reference",
        "reference-text-oid",
        "reference-text-uri",
        "reference-text-url",
        "reference-text-uuid",
        "reference-text-advanced-canonical",
        "reference-text-advanced-reference",
        "reference-text-advanced-oid",
        "reference-text-advanced-uri",
        "reference-text-advanced-url",
        "reference-text-advanced-uuid",
        "token-above-code",
        "token-above-coding",
        "token-below-code",
        "token-below-coding",
        "token-code-text-code",
        "token-code-text-coding",
        "token-in-code",
        "token-in-coding",
        "token-not-in-code",
        "token-not-in-coding",
        "token-of-type-code",
        "token-of-type-coding",
        "token-text-code",
        "token-text-coding",
        "token-text-advanced-code",
        "token-text-advanced-coding",
        "token-above-codeableconcept",
        "token-above-identifier",
        "token-above-contactpoint",
        "token-above-canonical",
        "token-above-oid",
        "token-above-uri",
        "token-above-url",
        "token-above-uuid",
        "token-above-string",
        "token-below-codeableconcept",
        "token-below-identifier",
        "token-below-contactpoint",
        "token-below-canonical",
        "token-below-oid",
        "token-below-uri",
        "token-below-url",
        "token-below-uuid",
        "token-below-string",
        "token-code-text-codeableconcept",
        "token-code-text-identifier",
        "token-code-text-contactpoint",
        "token-code-text-canonical",
        "token-code-text-oid",
        "token-code-text-uri",
        "token-code-text-url",
        "token-code-text-uuid",
        "token-code-text-string",
        "token-in-codeableconcept",
        "token-in-identifier",
        "token-in-contactpoint",
        "token-in-canonical",
        "token-in-oid",
        "token-in-uri",
        "token-in-url",
        "token-in-uuid",
        "token-in-string",
        "token-not-codeableconcept",
        "token-not-in-codeableconcept",
        "token-not-in-identifier",
        "token-not-in-contactpoint",
        "token-not-in-canonical",
        "token-not-in-oid",
        "token-not-in-uri",
        "token-not-in-url",
        "token-not-in-uuid",
        "token-not-in-string",
        "token-of-type-codeableconcept",
        "token-of-type-identifier",
        "token-of-type-contactpoint",
        "token-of-type-canonical",
        "token-of-type-oid",
        "token-of-type-uri",
        "token-of-type-url",
        "token-of-type-uuid",
        "token-of-type-string",
        "token-text-codeableconcept",
        "token-text-identifier",
        "token-text-contactpoint",
        "token-text-canonical",
        "token-text-oid",
        "token-text-uri",
        "token-text-url",
        "token-text-uuid",
        "token-text-string",
        "token-text-advanced-codeableconcept",
        "token-text-advanced-identifier",
        "token-text-advanced-contactpoint",
        "token-text-advanced-canonical",
        "token-text-advanced-oid",
        "token-text-advanced-uri",
        "token-text-advanced-url",
        "token-text-advanced-uuid",
        "token-text-advanced-string",
        "uri-above-canonical",
        "uri-above-oid",
        "uri-above-uri",
        "uri-above-url",
        "uri-above-uuid",
        "uri-below-canonical",
        "uri-below-oid",
        "uri-below-uri",
        "uri-below-url",
        "uri-below-uuid",
        "uri-contains-canonical",
        "uri-contains-oid",
        "uri-contains-uri",
        "uri-contains-url",
        "uri-contains-uuid",
        "uri-in-canonical",
        "uri-in-oid",
        "uri-in-uri",
        "uri-in-url",
        "uri-in-uuid",
        "uri-not-canonical",
        "uri-not-oid",
        "uri-not-uri",
        "uri-not-url",
        "uri-not-uuid",
        "uri-not-in-canonical",
        "uri-not-in-oid",
        "uri-not-in-uri",
        "uri-not-in-url",
        "uri-not-in-uuid",
        "uri-of-type-canonical",
        "uri-of-type-oid",
        "uri-of-type-uri",
        "uri-of-type-url",
        "uri-of-type-uuid",
        "uri-text-canonical",
        "uri-text-oid",
        "uri-text-uri",
        "uri-text-url",
        "uri-text-uuid",
        "uri-text-advanced-canonical",
        "uri-text-advanced-oid",
        "uri-text-advanced-uri",
        "uri-text-advanced-url",
        "uri-text-advanced-uuid",
    };

    /// <summary>Query if 'modifier' is modifier valid for type.</summary>
    /// <param name="modifier">The modifier.</param>
    /// <param name="type">    The type.</param>
    /// <returns>True if modifier valid for type, false if not.</returns>
    public static bool IsModifierValidForType(SearchModifierCodes modifier, SearchParamType type)
    {
        return type switch
        {
            SearchParamType.Date => ModifiersForDate.Contains(modifier),
            SearchParamType.Number => ModifiersForNumber.Contains(modifier),
            SearchParamType.Quantity => ModifiersForQuantity.Contains(modifier),
            SearchParamType.Reference => ModifiersForReference.Contains(modifier),
            SearchParamType.String => ModifiersForString.Contains(modifier),
            SearchParamType.Token => ModifiersForToken.Contains(modifier),
            SearchParamType.Uri => ModifiersForUri.Contains(modifier),
            _ => false,
        };
    }
}
