// <copyright file="TourUtils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Ui.R4.Subscriptions;

/// <summary>Utilities and constants for the Subscriptions Tour.</summary>
public static class TourUtils
{
    public const string EncounterJson = """"
{
  "resourceType": "Encounter",
  "status": "finished",
  "class": {
    "system": "http://terminology.hl7.org/CodeSystem/v3-ActCode",
    "code": "VR",
    "display": "virtual"
  },
  "subject": { "reference": "Patient/example" }
}
"""";

}
