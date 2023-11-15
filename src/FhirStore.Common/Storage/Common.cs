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

    /// <summary>A parsed interaction.</summary>
    public readonly record struct ParsedInteraction()
    {
        /// <summary>Gets or initializes the interaction.</summary>
        public StoreInteractionCodes? Interaction { get; init; } = null;

        /// <summary>Gets or initializes a message describing the error.</summary>
        public string ErrorMessage { get; init; } = string.Empty;

        /// <summary>Gets or initializes the full pathname of the URL file.</summary>
        public string UrlPath { get; init; } = string.Empty;

        /// <summary>Gets or initializes the URL query.</summary>
        public string UrlQuery { get; init; } = string.Empty;

        /// <summary>Gets or initializes the type of the resource.</summary>
        public string ResourceType { get; init; } = string.Empty;

        /// <summary>Gets or initializes the identifier.</summary>
        public string Id { get; init; } = string.Empty;

        /// <summary>Gets or initializes the name of the operation.</summary>
        public string OperationName { get; init; } = string.Empty;

        /// <summary>Gets or initializes the type of the compartment.</summary>
        public string CompartmentType { get; init; } = string.Empty;

        /// <summary>Gets or initializes the version.</summary>
        public string Version { get; init; } = string.Empty;
    }
}
