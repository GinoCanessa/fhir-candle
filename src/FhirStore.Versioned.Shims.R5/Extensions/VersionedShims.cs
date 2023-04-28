// <copyright file="VersionedShims.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using System.Linq;

namespace FhirStore.Versioned.Shims.Extensions;

/// <summary>A versioned shims.</summary>
public static class VersionedShims
{
    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType[]? CopyTargetsToRt(IEnumerable<VersionIndependentResourceTypesAll?>? targets)
    {
        return targets?
            .Where<VersionIndependentResourceTypesAll?>(r => (r != null) && (ModelInfo.FhirTypeToCsType.ContainsKey(r.ToString())))
            .Select(r => (ResourceType)ModelInfo.FhirTypeNameToResourceType(r.ToString()!)!)
            .ToArray<ResourceType>() ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static VersionIndependentResourceTypesAll?[]? CopyTargetsNullable(IEnumerable<ResourceType>? targets)
    {
        return targets?.Select(r => (VersionIndependentResourceTypesAll?)r).ToArray() ?? null;
    }
}
