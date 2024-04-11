// <copyright file="IIndexContent.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Microsoft.AspNetCore.Components;

namespace fhir.candle.Models;

/// <summary>Information about the index content.</summary>
/// <param name="ContentFor"> The content for package.</param>
/// <param name="FhirVersionLiteral">The FHIR version literal.</param>
public record struct IndexContentInfo(
    string ContentFor,
    string FhirVersionLiteral);

/// <summary>Interface for index content.</summary>
public interface IIndexContent
{
    /// <summary>Gets the content for package.</summary>
    public static virtual string ContentFor => string.Empty;

    /// <summary>Gets the FHIR version literal.</summary>
    public static virtual string FhirVersionLiteral => string.Empty;
}