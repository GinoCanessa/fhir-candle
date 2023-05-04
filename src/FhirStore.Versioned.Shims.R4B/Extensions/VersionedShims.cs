// <copyright file="VersionedShims.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;

namespace FhirStore.Versioned.Shims.Extensions;

/// <summary>A versioned shims.</summary>
public static class VersionedShims
{
    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType[]? CopyTargets(IEnumerable<ResourceType?>? targets)
    {
        return targets?.Where(r => r != null).Select(r => (ResourceType)r!).ToArray() ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType[]? CopyTargetsToRt(IEnumerable<ResourceType?>? targets)
    {
        return targets?.Where(r => r != null).Select(r => (ResourceType)r!).ToArray() ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType?[]? CopyTargetsNullable(IEnumerable<ResourceType?>? targets)
    {
        return targets?.Select(r => (ResourceType?)r).ToArray() ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType?[]? CopyTargetsNullable(IEnumerable<ResourceType>? targets)
    {
        return targets?.Select(r => (ResourceType?)r).ToArray() ?? null;
    }

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static ResourceType[]? CopyTargets(IEnumerable<ResourceType>? targets)
    {
        return targets?.Select(r => (ResourceType)r).ToArray() ?? null;
    }
}
