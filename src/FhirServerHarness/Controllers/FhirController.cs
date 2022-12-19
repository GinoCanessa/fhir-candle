// <copyright file="FhirControllerR4.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Services;
using Microsoft.AspNetCore.Mvc;

namespace FhirServerHarness.Controllers;

/// <summary>A FHIR R4 controller.</summary>
public class FhirController : Controller
{
    IFhirStoreManager _fhirStoreManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirController"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="fhirStore">The FHIR store.</param>
    public FhirController([FromServices] IFhirStoreManager fhirStoreManager)
    {
        if (fhirStoreManager == null)
        {
            throw new ArgumentNullException(nameof(fhirStoreManager));
        }

        _fhirStoreManager = fhirStoreManager;
    }

    public IActionResult Index()
    {
        return View();
    }
}
