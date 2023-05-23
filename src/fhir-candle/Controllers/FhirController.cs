// <copyright file="FhirController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using fhir.candle.Services;
using FhirStore.Storage;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace fhir.candle.Controllers;

/// <summary>A FHIR API controller.</summary>
[ApiController]
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
        //"json",
        //"xml",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirController"/> class.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when one or more required arguments are null.</exception>
    /// <param name="fhirStore">The FHIR store.</param>
    public FhirController([FromServices] IFhirStoreManager fhirStoreManager)            // , [FromServices] IServer host
    {
        if (fhirStoreManager == null)
        {
            throw new ArgumentNullException(nameof(fhirStoreManager));
        }

        _fhirStoreManager = fhirStoreManager;

        //if (host != null)
        //{
        //    ICollection<string> addresses = host.Features?.Get<IServerAddressesFeature>()?.Addresses ?? Array.Empty<string>();
        //    foreach (string address in addresses)
        //    {
        //        Console.WriteLine($"Listening on: {address}");
        //    }
        //}

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

        return "application/fhir+json";
    }

    private async Task AddBody(HttpResponse response, string? prefer, string resource, string outcome)
    {
        switch (prefer)
        {
            case "return=minimal":
                break;

            default:
            case "return=representation":
                if (!string.IsNullOrEmpty(resource))
                {
                    await response.WriteAsync(resource);
                }
                break;

            case "return=OperationOutcome":
                if (!string.IsNullOrEmpty(outcome))
                {
                    await response.WriteAsync(outcome);
                }
                break;
        }
    }

    [HttpGet, Route("{store}/metadata")]
    public async Task GetMetadata(
        [FromRoute] string store,
        [FromQuery(Name = "_format")] string? format)
    {
        if (!_fhirStoreManager.ContainsKey(store))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, Request);
        //string format = GetMimeType(string.Empty, HttpContext.Request);

        HttpStatusCode sc = _fhirStoreManager[store].GetMetadata(
            format,
            out string resource,
            out string outcome,
            out string eTag,
            out string lastModified);

        if (!string.IsNullOrEmpty(eTag))
        {
            Response.Headers.Add(HeaderNames.ETag, eTag);
        }

        if (!string.IsNullOrEmpty(lastModified))
        {
            Response.Headers.Add(HeaderNames.LastModified, lastModified);
        }

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        if (!string.IsNullOrEmpty(resource))
        {
            await Response.WriteAsync(resource);
        }
    }

    [HttpGet, Route("{store}/{resourceName}/{id}")]
    public async Task GetResourceInstanceOrOperation(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc;
        string resource, outcome, eTag, lastModified;

        if (id[0] == '$')
        {
            // operation
            sc = _fhirStoreManager[store].TypeOperation(
                resourceName,
                id,
                Request.QueryString.ToString(),
                string.Empty,
                string.Empty,
                format,
                out resource,
                out outcome);
        }
        else
        {
            // read instance

            sc = _fhirStoreManager[store].InstanceRead(
                resourceName,
                id,
                format,
                summary ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                out resource,
                out outcome,
                out eTag,
                out lastModified);

            if (!string.IsNullOrEmpty(eTag))
            {
                Response.Headers.Add(HeaderNames.ETag, eTag);
            }

            if (!string.IsNullOrEmpty(lastModified))
            {
                Response.Headers.Add(HeaderNames.LastModified, lastModified);
            }
        }

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        if (!string.IsNullOrEmpty(resource))
        {
            await Response.WriteAsync(resource);
        }
    }

    [HttpPost, Route("{store}/{resourceName}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostResourceType(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromHeader(Name = "Prefer")] string? prefer)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            System.Console.WriteLine("PostResourceType <<< cannot process a POST without data!");
            Response.StatusCode = 400;
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
                    Request.ContentType ?? string.Empty,
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
                    Response.Headers.Add(HeaderNames.ETag, eTag);
                }

                if (!string.IsNullOrEmpty(lastModified))
                {
                    Response.Headers.Add(HeaderNames.LastModified, lastModified);
                }

                if (!string.IsNullOrEmpty(location))
                {
                    Response.Headers.Add(HeaderNames.Location, location);
                }

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"PostResourceType <<< caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($" <<< inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
            return;
        }
    }


    [HttpPut, Route("{store}/{resourceName}/{id}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PutResourceInstance(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromHeader(Name = "Prefer")] string? prefer)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, Request);

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            System.Console.WriteLine("PutResourceInstance <<< cannot process a PUT without data!");
            Response.StatusCode = 400;
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
                    Response.Headers.Add(HeaderNames.ETag, eTag);
                }

                if (!string.IsNullOrEmpty(lastModified))
                {
                    Response.Headers.Add(HeaderNames.LastModified, lastModified);
                }

                if (!string.IsNullOrEmpty(location))
                {
                    Response.Headers.Add(HeaderNames.Location, location);
                }

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"PutResourceInstance <<< caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($" <<< inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
            return;
        }
    }

    [HttpDelete, Route("{store}/{resourceName}/{id}")]
    public async Task DeleteResourceInstance(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromHeader(Name = "Prefer")] string? prefer)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc = _fhirStoreManager[store].InstanceDelete(
            resourceName,
            id,
            format,
            string.Empty,
            out string resource,
            out string outcome);

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
    }

    [HttpGet, Route("{store}/{resourceName}")]
    public async Task GetResourceTypeSearch(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc = _fhirStoreManager[store].TypeSearch(
            resourceName,
            Request.QueryString.ToString(),
            format,
            out string results,
            out string outcome);

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        if (!string.IsNullOrEmpty(results))
        {
            await Response.WriteAsync(results);
        }
    }

    [HttpPost, Route("{store}/{resourceName}/_search")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task PostResourceTypeSearch(
        [FromRoute] string store,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary)
    {
        if ((!_fhirStoreManager.ContainsKey(store)) ||
            (!_fhirStoreManager[store].SupportsResource(resourceName)))
        {
            Response.StatusCode = 404;
            return;
        }

        format = GetMimeType(format, Request);

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            System.Console.WriteLine("PostResourceTypeSearch <<< cannot process a PUT without data!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                HttpStatusCode sc = _fhirStoreManager[store].TypeSearch(
                    resourceName,
                    content,
                    format,
                    out string results,
                    out string outcome);

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                if (!string.IsNullOrEmpty(results))
                {
                    await Response.WriteAsync(results);
                }
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"PostResourceTypeSearch <<< caught: {ex.Message}");
            if (ex.InnerException != null)
            {
                System.Console.WriteLine($" <<< inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
            return;
        }
    }
}
