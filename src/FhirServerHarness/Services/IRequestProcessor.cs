// <copyright file="IRequestProcessor.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Microsoft.AspNetCore.Http;

namespace FhirServerHarness.Services;

public interface IRequestProcessor : IDisposable
{
    /// <summary>Process the request asynchronous described by context.</summary>
    /// <param name="context">The context.</param>
    /// <returns>An asynchronous result.</returns>
    Task ProcessRequestAsync(HttpContext context);
}
