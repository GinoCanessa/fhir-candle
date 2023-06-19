// <copyright file="ResourceTypeExtensions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;

namespace FhirStore.Versioned.Extensions;

/// <summary>Resource Type extensions for version-specific FHIR stores.</summary>
public static class ResourceTypeExtensions
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

    /// <summary>Copies the targets described by targets.</summary>
    /// <param name="targets">The targets.</param>
    /// <returns>A ResourceType[]?</returns>
    public static IEnumerable<ResourceType?> CopyTargetsNullable(this IEnumerable<string>? targets)
    {
        List<ResourceType?> resourceTypes = new();

        if (targets == null)
        {
            return resourceTypes.AsEnumerable();
        }

        foreach (string r in targets)
        {
            try
            {
                ResourceType? rt = Hl7.Fhir.Utility.EnumUtility.ParseLiteral<ResourceType>(r);
                if (rt != null)
                {
                    resourceTypes.Add(rt!);
                }
            }
            catch (Exception)
            {
            }
        }

        return resourceTypes.AsEnumerable();
    }
}
