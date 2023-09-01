// <copyright file="IRiPage.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>Information about the ri page.</summary>
/// <param name="ContentForPackage"> The content for package.</param>
/// <param name="PageName">          Name of the page.</param>
/// <param name="Description">       The description.</param>
/// <param name="RoutePath">         Full pathname of the route file.</param>
/// <param name="FhirVersionLiteral">The FHIR version literal.</param>
/// <param name="FhirVersionNumeric">The FHIR version numeric.</param>
public record struct RiPageInfo(
    string ContentForPackage, 
    string PageName, 
    string Description, 
    string RoutePath, 
    string FhirVersionLiteral, 
    string FhirVersionNumeric);

/// <summary>Interface for RI pages.</summary>
public interface IRiPage
{
    /// <summary>Gets the content for package.</summary>
    public static string ContentForPackage { get => string.Empty; }

    /// <summary>Gets the name of the page.</summary>
    public static string PageName { get => string.Empty; }

    /// <summary>Gets the description.</summary>
    public static string Description { get => string.Empty; }

    /// <summary>Gets the full pathname of the route file.</summary>
    public static string RoutePath { get => string.Empty; }

    /// <summary>Gets the FHIR version literal.</summary>
    public static string FhirVersionLiteral { get => string.Empty; }

    /// <summary>Gets the FHIR version numeric.</summary>
    public static string FhirVersionNumeric { get => string.Empty; }
}

/// <summary>Interface for FHIR R4 RI Pages.</summary>
public interface IRiPageR4 : IRiPage 
{
    /// <summary>Gets the FHIR version literal.</summary>
    public static new string FhirVersionLiteral { get => "R4"; }

    /// <summary>Gets the FHIR version numeric.</summary>
    public static new string FhirVersionNumeric { get => "4.0"; }
}

/// <summary>Interface for FHIR R4B RI Pages.</summary>
public interface IRiPageR4B : IRiPage
{
    /// <summary>Gets the FHIR version literal.</summary>
    public static new string FhirVersionLiteral { get => "R4B"; }

    /// <summary>Gets the FHIR version numeric.</summary>
    public static new string FhirVersionNumeric { get => "4.3"; }
}

/// <summary>Interface for FHIR R5 RI Pages.</summary>
public interface IRiPageR5 : IRiPage
{
    /// <summary>Gets the FHIR version literal.</summary>
    public static new string FhirVersionLiteral { get => "R5"; }

    /// <summary>Gets the FHIR version numeric.</summary>
    public static new string FhirVersionNumeric { get => "5.0"; }
}

