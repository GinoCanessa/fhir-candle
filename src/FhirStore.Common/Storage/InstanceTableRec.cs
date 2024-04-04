// <copyright file="InstanceTableRec.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Storage;

/// <summary>
/// Represents a record in the instance table.
/// </summary>
public record class InstanceTableRec
{
    /// <summary>
    /// Gets or sets the ID of the instance.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets or sets the name of the instance.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the URL of the instance.
    /// </summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the instance.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifiers of the instance.
    /// </summary>
    public string Identifiers { get; init; } = string.Empty;
}
