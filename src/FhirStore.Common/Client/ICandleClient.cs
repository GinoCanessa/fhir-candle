// <copyright file="CandleClient.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Client;

namespace FhirStore.Client;

/// <summary>Interface for candle client.</summary>
public interface ICandleClient
{
    /// <summary>Gets or initializes URL of the FHIR server.</summary>
    string FhirServerUrl { get; init; }

    /// <summary>Gets or initializes options for controlling this client.</summary>
    CandleClientSettings Settings { get; init; }
}
