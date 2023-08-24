// <copyright file="Common.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.RegularExpressions;

namespace FhirCandle.Storage;

/// <summary>Common functionality for FHIR Stores.</summary>
public static partial class Common
{

    /// <summary>Values that represent store interactions.</summary>
    public enum StoreInteractionCodes
    {
        CompartmentOperation,
        CompartmentSearch,
        CompartmentTypeSearch,

        InstanceDelete,
        InstanceDeleteHistory,
        InstanceDeleteVersion,
        InstanceOperation,
        InstancePatch,
        InstanceRead,
        InstanceReadHistory,
        InstanceReadVersion,
        InstanceUpdate,

        TypeCreate,
        TypeCreateConditional,
        TypeDeleteConditional,
        TypeHistory,
        TypeOperation,
        TypeSearch,

        SystemCapabilities,
        SystemBundle,
        SystemDeleteConditional,
        SystemHistory,
        SystemOperation,
        SystemSearch,
    }
}
