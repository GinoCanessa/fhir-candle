// <copyright file="CandleClient.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Rest;

namespace FhirCandle.Client;

public class CandleClient
{
    private FhirClient _client = null!;

    public CandleClient()
    {
    }

    public required string FhirServerUrl { get; init; }

    public string SmartClientId { get; init; } = "fhir_candle_client";

    public string SmartRedirectUrl { get; init; } = string.Empty;

    public IEnumerable<string> SmartScopes { get; init; } = Enumerable.Empty<string>();
}
