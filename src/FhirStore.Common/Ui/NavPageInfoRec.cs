// <copyright file="NavPageInfoRec.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirCandle.Ui;

/// <summary>Information about the navigation page information.</summary>
public readonly record struct NavPageInfoRec
{
    /// <summary>Gets or initializes the display.</summary>
    public required string Display { get; init; }

    /// <summary>Gets or initializes the link.</summary>
    public required string Link { get; init; }
}
