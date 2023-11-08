// <copyright file="IRiPage.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Models;

/// <summary>Information about the package page.</summary>
/// <param name="ContentFor">        The content for.</param>
/// <param name="PageName">          Name of the page.</param>
/// <param name="Description">       The description.</param>
/// <param name="RoutePath">         Full pathname of the route file.</param>
/// <param name="FhirVersionLiteral">The FHIR version literal.</param>
/// <param name="FhirVersionNumeric">The FHIR version numeric.</param>
public record struct PackagePageInfo(
    string ContentFor, 
    string PageName, 
    string Description, 
    string RoutePath, 
    string FhirVersionLiteral, 
    string FhirVersionNumeric);

/// <summary>Interface for package/ri pages.</summary>
public interface IPackagePage
{
    /// <summary>Gets the package or ri name this page is for.</summary>
    public virtual static string ContentFor { get => string.Empty; }

    /// <summary>Gets the name of the page.</summary>
    public virtual static string PageName { get => string.Empty; }

    /// <summary>Gets the description.</summary>
    public virtual static string Description { get => string.Empty; }

    /// <summary>Gets the full pathname of the route file.</summary>
    public virtual static string RoutePath { get => string.Empty; }

    /// <summary>Gets the FHIR version literal.</summary>
    public virtual static string FhirVersionLiteral { get => string.Empty; }

    /// <summary>Gets the FHIR version numeric.</summary>
    public virtual static string FhirVersionNumeric { get => string.Empty; }
}
