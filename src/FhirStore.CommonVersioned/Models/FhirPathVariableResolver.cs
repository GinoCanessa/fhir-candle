// <copyright file="FhirPathVariableResolver.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.ElementModel;

namespace FhirStore.Models;

/// <summary>A FHIR path variable resolver.</summary>
public class FhirPathVariableResolver
{
    /// <summary>(Immutable) The FHIR path prefix.</summary>
    public const string _fhirPathPrefix = "/_fpvar/";

    /// <summary>(Immutable) Length of the FHIR path prefix.</summary>
    private const int _fhirPathPrefixLength = 8;

    /// <summary>Gets the versioned FHIR store.</summary>
    public required Func<string, ITypedElement> NextResolver { get; init; }

    /// <summary>Gets or initializes the variables.</summary>
    public Dictionary<string, ITypedElement> Variables { get; init; } = new();

    /// <summary>Resolves the given document.</summary>
    /// <param name="uri">URI of the resource.</param>
    /// <returns>An ITypedElement.</returns>
    public ITypedElement Resolve(string uri)
    {
        if (uri.StartsWith(_fhirPathPrefix, StringComparison.Ordinal))
        {
            string name = uri.Substring(_fhirPathPrefixLength);

            if (Variables.ContainsKey(name))
            {
                return Variables[name];
            }

            return null!;
        }

        return NextResolver(uri);
    }
}
