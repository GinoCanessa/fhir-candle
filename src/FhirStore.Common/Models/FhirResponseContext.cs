// <copyright file="FhirResponseContext.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;

namespace FhirCandle.Models;

/// <summary>A FHIR response context.</summary>
public record class FhirResponseContext
{
    /// <summary>Gets or initializes the status code.</summary>
    public HttpStatusCode? StatusCode { get; init; }

    /// <summary>Gets or initializes the serialized format.</summary>
    public string MimeType { get; init; } = string.Empty;

    /// <summary>Gets or initializes the resource.</summary>
    public object? Resource { get; init; }

    /// <summary>Gets or initializes the type of the resource.</summary>
    public string ResourceType { get; init; } = string.Empty;

    /// <summary>Gets or initializes the identifier.</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Gets or initializes the serialized resource.</summary>
    public string SerializedResource { get; init; } = string.Empty;

    /// <summary>Gets or initializes the OperationOutcome.</summary>
    public object? Outcome { get; init; }

    /// <summary>Gets or initializes the serialized outcome.</summary>
    public string SerializedOutcome { get; init; } = string.Empty;

    /// <summary>Gets or initializes information describing a non-FHIR response.</summary>
    public object? NonFhirData { get; init; }

    /// <summary>Gets or initializes the tag.</summary>
    public string ETag { get; init; } = string.Empty;

    /// <summary>Gets or initializes the location.</summary>
    public string Location { get; init; } = string.Empty;

    /// <summary>Gets or initializes the last modified.</summary>
    public string LastModified { get; init; } = string.Empty;
}
