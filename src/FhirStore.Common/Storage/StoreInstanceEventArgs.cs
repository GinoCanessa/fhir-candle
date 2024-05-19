// <copyright file="StoreInstanceEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


namespace FhirCandle.Storage;

public class StoreInstanceEventArgs : EventArgs
{
    /// <summary>Gets or initializes the type of the resource.</summary>
    public required string ResourceType { get; init; }

    /// <summary>Gets or initializes the identifier of the resource.</summary>
    public required string ResourceId { get; init; }
}
