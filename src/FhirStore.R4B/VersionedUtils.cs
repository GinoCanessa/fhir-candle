// <copyright file="VersionedUtils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Versioned;

/// <summary>A versioned utilities.</summary>
internal static class VersionedUtils
{
    /// <summary>Gets the type to use for int64 elements.</summary>
    internal static Hl7.Fhir.Model.FHIRAllTypes GetInt64Type => Hl7.Fhir.Model.FHIRAllTypes.String;
}
