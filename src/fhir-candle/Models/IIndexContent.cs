// <copyright file="IIndexContent.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Microsoft.AspNetCore.Components;

namespace fhir.candle.Models;

/// <summary>Information about the index content.</summary>
/// <param name="ContentForPackage"> The content for package.</param>
/// <param name="FhirVersionLiteral">The FHIR version literal.</param>
public record struct IndexContentInfo(
    string ContentForPackage,
    string FhirVersionLiteral);

/// <summary>Interface for index content.</summary>
public interface IIndexContent
{
    /// <summary>Gets the content for package.</summary>
    public static string ContentForPackage { get => string.Empty; }

    /// <summary>Gets the FHIR version literal.</summary>
    public static string FhirVersionLiteral { get => string.Empty; }
}

/// <summary>Interface for index content r 4.</summary>
public interface IIndexContentR4 : IIndexContent
{
    /// <summary>Gets the FHIR version literal.</summary>
    public static new string FhirVersionLiteral { get => "R4"; }
}

/// <summary>Interface for index content r 4.</summary>
public interface IIndexContentR4B : IIndexContent
{
    /// <summary>Gets the FHIR version literal.</summary>
    public static new string FhirVersionLiteral { get => "R4B"; }
}

/// <summary>Interface for index content r 4.</summary>
public interface IIndexContentR5 : IIndexContent
{
    /// <summary>Gets the FHIR version literal.</summary>
    public static new string FhirVersionLiteral { get => "R5"; }
}
