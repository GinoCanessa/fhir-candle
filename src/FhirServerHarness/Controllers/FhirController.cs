// <copyright file="FhirController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using FhirServerHarness.Services;
using FhirStore.Common.Storage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace FhirServerHarness.Controllers;


/// <summary>A FHIR R4 controller.</summary>
[Route("fhir", Order = 1)]
[Produces("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
public class FhirController : ControllerBase
{
    private IFhirStoreManager _fhirStoreManager;

    private readonly HashSet<string> _acceptMimeTypes = new()
    {
        "application/fhir+json",
        "application/fhir+xml",
        "application/json",
        "application/xml",
        "json",
        "xml",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirController"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="fhirStore">The FHIR store.</param>
    public FhirController([FromServices] IFhirStoreManager fhirStoreManager)
    {
        if (fhirStoreManager == null)
        {
            throw new ArgumentNullException(nameof(fhirStoreManager));
        }

        _fhirStoreManager = fhirStoreManager;
    }

    private string GetMimeType(string? queryParam, HttpRequest request)
    {
        if (!string.IsNullOrEmpty(queryParam))
        {
            if (_acceptMimeTypes.Contains(queryParam))
            {
                return queryParam;
            }

            if (queryParam.Contains(' '))
            {
                queryParam = queryParam.Replace(' ', '+');
            }

            if (_acceptMimeTypes.Contains(queryParam))
            {
                return queryParam;
            }
        }

        foreach (string? accept in request.Headers?.Accept ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(accept))
            {
                continue;
            }

            if (!_acceptMimeTypes.Contains(accept))
            {
                continue;
            }

            return accept;
        }

        return "application/json";
    }

    [HttpGet, Route("{store}/metadata")]
    public async Task GetMetadata(
        [FromRoute] string store,
        [FromQuery(Name = "_format")] string? format)
    {
        if (!_fhirStoreManager.ContainsKey(store))
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);
        //string format = GetMimeType(string.Empty, HttpContext.Request);

        HttpStatusCode sc = _fhirStoreManager[store].GetMetadata(
            format,
            out string resource,
            out string outcome,
            out string eTag,
            out string lastModified);

        if (!string.IsNullOrEmpty(eTag))
        {
            HttpContext.Response.Headers.Add(HeaderNames.ETag, eTag);
        }

        if (!string.IsNullOrEmpty(lastModified))
        {
            HttpContext.Response.Headers.Add(HeaderNames.LastModified, lastModified);
        }

        HttpContext.Response.ContentType = format;
        HttpContext.Response.StatusCode = (int)sc;

        if (!string.IsNullOrEmpty(resource))
        {
            await HttpContext.Response.WriteAsync(resource);
        }
    }

    [HttpGet, Route("{store}/{resourceName}/{id}")]
    public async Task GetResourceInstance(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

        HttpStatusCode sc = _fhirStoreManager[store].InstanceRead(
            resourceName,
            id,
            format,
            summary ?? string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            out string resource,
            out string outcome,
            out string eTag,
            out string lastModified);

        if (!string.IsNullOrEmpty(eTag))
        {
            HttpContext.Response.Headers.Add(HeaderNames.ETag, eTag);
        }

        if (!string.IsNullOrEmpty(lastModified))
        {
            HttpContext.Response.Headers.Add(HeaderNames.LastModified, lastModified);
        }

        HttpContext.Response.ContentType = format;
        HttpContext.Response.StatusCode = (int)sc;

        if (!string.IsNullOrEmpty(resource))
        {
            await HttpContext.Response.WriteAsync(resource);
        }
    }

    [HttpPost, Route("{store}/{resourceName}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostResourceType(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            System.Console.WriteLine("PostResourceType <<< cannot process a POST without data!");
            HttpContext.Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                HttpStatusCode sc = _fhirStoreManager[store].InstanceCreate(
                    resourceName,
                    content,
                    HttpContext.Request.ContentType ?? string.Empty,
                    format,
                    string.Empty,
                    true,
                    out string resource,
                    out string outcome,
                    out string eTag,
                    out string lastModified,
                    out string location);

                if (!string.IsNullOrEmpty(eTag))
                {
                    HttpContext.Response.Headers.Add(HeaderNames.ETag, eTag);
                }

                if (!string.IsNullOrEmpty(lastModified))
                {
                    HttpContext.Response.Headers.Add(HeaderNames.LastModified, lastModified);
                }

                if (!string.IsNullOrEmpty(location))
                {
                    HttpContext.Response.Headers.Add(HeaderNames.Location, location);
                }

                HttpContext.Response.ContentType = format;
                HttpContext.Response.StatusCode = (int)sc;

                if (!string.IsNullOrEmpty(resource))
                {
                    await HttpContext.Response.WriteAsync(resource);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"HandlePost <<< caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($" <<< inner: {ex.InnerException.Message}");
            }

            HttpContext.Response.StatusCode = 500;
            return;
            //return StatusCode(500, ex.Message);
        }
    }


    [HttpPut, Route("{store}/{resourceName}/{id}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PutResourceInstance(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            System.Console.WriteLine("PutResourceInstance <<< cannot process a PUT without data!");
            HttpContext.Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                HttpStatusCode sc = _fhirStoreManager[store].InstanceUpdate(
                    resourceName,
                    id,
                    content,
                    HttpContext.Request.ContentType ?? string.Empty,
                    format,
                    string.Empty,
                    string.Empty,
                    true,
                    out string resource,
                    out string outcome,
                    out string eTag,
                    out string lastModified,
                    out string location);

                if (!string.IsNullOrEmpty(eTag))
                {
                    HttpContext.Response.Headers.Add(HeaderNames.ETag, eTag);
                }

                if (!string.IsNullOrEmpty(lastModified))
                {
                    HttpContext.Response.Headers.Add(HeaderNames.LastModified, lastModified);
                }

                if (!string.IsNullOrEmpty(location))
                {
                    HttpContext.Response.Headers.Add(HeaderNames.Location, location);
                }

                HttpContext.Response.ContentType = format;
                HttpContext.Response.StatusCode = (int)sc;

                if (!string.IsNullOrEmpty(resource))
                {
                    await HttpContext.Response.WriteAsync(resource);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"HandlePost <<< caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($" <<< inner: {ex.InnerException.Message}");
            }

            HttpContext.Response.StatusCode = 500;
            return;
            //return StatusCode(500, ex.Message);
        }
    }


    [HttpDelete, Route("{store}/{resourceName}/{id}")]
    public async Task DeleteResourceInstance(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

        HttpStatusCode sc = _fhirStoreManager[store].InstanceDelete(
            resourceName,
            id,
            format,
            string.Empty,
            out string resource,
            out string outcome);

        HttpContext.Response.ContentType = format;
        HttpContext.Response.StatusCode = (int)sc;

        if (!string.IsNullOrEmpty(resource))
        {
            await HttpContext.Response.WriteAsync(resource);
        }
    }

}
