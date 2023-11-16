// <copyright file="FhirController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using fhir.candle.Services;
using FhirCandle.Models;
using FhirCandle.Storage;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace fhir.candle.Controllers;

/// <summary>A FHIR API controller.</summary>
[ApiController]
[Route("fhir", Order = 2)]
[Produces("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
public class FhirController : ControllerBase
{
    private IFhirStoreManager _fhirStoreManager;
    private ISmartAuthManager _smartAuthManager;

    private ILogger<FhirController> _logger;

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
    public FhirController(
        [FromServices] IFhirStoreManager fhirStoreManager,
        [FromServices] ISmartAuthManager smartAuthManager,
        [FromServices] ILogger<FhirController> logger)
    {
        if (fhirStoreManager == null)
        {
            throw new ArgumentNullException(nameof(fhirStoreManager));
        }

        _fhirStoreManager = fhirStoreManager;

        if (smartAuthManager == null)
        {
            throw new ArgumentNullException(nameof(smartAuthManager));
        }

        _smartAuthManager = smartAuthManager;

        _logger = logger;

        //if (host != null)
        //{
        //    ICollection<string> addresses = host.Features?.Get<IServerAddressesFeature>()?.Addresses ?? Array.Empty<string>();
        //    foreach (string address in addresses)
        //    {
        //        Console.WriteLine($"Listening on: {address}");
        //    }
        //}

    }

    /// <summary>Gets the MIME type.</summary>
    /// <param name="queryParam">The query parameter.</param>
    /// <param name="request">   The request.</param>
    /// <returns>The mime type.</returns>
    private string GetMimeType(string? queryParam, HttpRequest request, bool allowNonFhirReturn = false)
    {
        if (!string.IsNullOrEmpty(queryParam))
        {
            if (_acceptMimeTypes.Contains(queryParam) ||
                allowNonFhirReturn)
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

            if (_acceptMimeTypes.Contains(accept) ||
                allowNonFhirReturn)
            {
                return accept;
            }
        }

        return "application/fhir+json";
    }

    /// <summary>Adds a body.</summary>
    /// <param name="response">The response.</param>
    /// <param name="prefer">  The prefer.</param>
    /// <param name="resource">The resource.</param>
    /// <param name="outcome"> The outcome.</param>
    /// <returns>An asynchronous result.</returns>
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
                else if (!string.IsNullOrEmpty(outcome))
                {
                    await response.WriteAsync(outcome);
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

    /// <summary>Logs and return error.</summary>
    /// <param name="response">The response.</param>
    /// <param name="code">    The code.</param>
    /// <param name="msg">     The message.</param>
    /// <returns>An asynchronous result.</returns>
    private async Task LogAndReturnError(HttpResponse response, int code, string msg)
    {
        _logger.LogError(msg);

        response.StatusCode = code;
        response.ContentType = "text/plain";
        await response.WriteAsync(msg);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets smart well known.</summary>
    /// <remarks>
    /// Note that most "core" SMART stuff is in the <see cref="SmartController"/>, but this needs to
    /// remain here to be discoverable by FHIR clients.
    /// </remarks>
    /// <param name="storeName">The store.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/.well-known/smart-configuration")]
    [Produces("application/json")]
    public async Task GetSmartWellKnown(
        [FromRoute] string storeName)
    {
        // make sure this store exists and has SMART enabled
        if (!_smartAuthManager.SmartConfigurationByTenant.TryGetValue(
                storeName, 
                out FhirStore.Smart.SmartWellKnown? smartConfig))
        {
            await LogAndReturnError(Response, 404, $"GetSmartWellKnown <<< no SMART config for {storeName}!");
            return;
        }

        // SMART well-known configuration is always returned as JSON
        Response.ContentType = "application/json";
        Response.StatusCode = (int)HttpStatusCode.OK;

        await Response.WriteAsync(FhirCandle.Serialization.SerializationCommon.SerializeObject(smartConfig));
    }

    /// <summary>(An Action that handles HTTP GET requests) gets a metadata.</summary>
    /// <param name="storeName">The store.</param>
    /// <param name="format">   Describes the format to use.</param>
    /// <param name="pretty">   The pretty.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/metadata")]
    public async Task GetMetadata(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"GetMetadata <<< no tenant at {storeName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.SystemCapabilities,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc = _fhirStoreManager[storeName].GetMetadata(
            ctx,
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

        Response.ContentType = ctx.DestinationFormat;
        Response.StatusCode = (int)sc;

        await AddBody(Response, null, resource, outcome);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets type operation.</summary>
    /// <param name="storeName">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}/${opName}")]
    public async Task GetTypeOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"GetTypeOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetTypeOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.TypeOperation,
            OperationName = "$" + opName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc = store.TypeOperation(
            ctx,
            out string resource,
            out string outcome);

        Response.ContentType = ctx.DestinationFormat;;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets resource instance.</summary>
    /// <param name="storeName">      The store.</param>
    /// <param name="resourceName">   Name of the resource.</param>
    /// <param name="id">             The identifier.</param>
    /// <param name="format">         Describes the format to use.</param>
    /// <param name="summary">        The summary.</param>
    /// <param name="pretty">         The pretty.</param>
    /// <param name="ifMatch">        A match specifying if.</param>
    /// <param name="ifModifiedSince">if modified since.</param>
    /// <param name="ifNoneMatch">    A match specifying if none.</param>
    /// <param name="authHeader">     The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}/{id}")]
    public async Task GetResourceInstance(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "If-Modified-Since")] string? ifModifiedSince,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"GetResourceInstance <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.InstanceRead,
            ResourceType = resourceName,
            Id = id,
            IfMatch = ifMatch ?? string.Empty,
            IfModifiedSince = ifModifiedSince ?? string.Empty,
            IfNoneMatch = ifNoneMatch ?? string.Empty,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc = store.InstanceRead(
            ctx,
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
 
        Response.ContentType = ctx.DestinationFormat;;
        Response.StatusCode = (int)sc;

        await AddBody(Response, null, resource, outcome);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets instance operation.</summary>
    /// <param name="storeName">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}/{id}/${opName}")]
    public async Task GetInstanceOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"GetInstanceOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetInstanceOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.InstanceOperation,
            ResourceType = resourceName,
            Id = id,
            OperationName = "$" + opName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc;
        string resource, outcome;

        // operation
        sc = store.InstanceOperation(
            ctx,
            out resource,
            out outcome);

        Response.ContentType = ctx.DestinationFormat;;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
    }

    /// <summary>
    /// (An Action that handles HTTP POST requests) posts an instance operation.
    /// </summary>
    /// <param name="store">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}/{id}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostInstanceOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostInstanceOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostInstanceOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        HttpStatusCode sc;
        string resource, outcome;

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Interaction = Common.StoreInteractionCodes.InstanceOperation,
                    ResourceType = resourceName,
                    Id = id,
                    OperationName = "$" + opName,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                // operation
                sc = store.InstanceOperation(
                    ctx,
                    out resource,
                    out outcome);

                Response.ContentType = ctx.DestinationFormat; ;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostInstanceOperation <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostInstanceOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }


    /// <summary>
    /// (An Action that handles HTTP POST requests) posts a resource type search.
    /// </summary>
    /// <param name="storeName">       The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="summary">     The summary.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}/_search")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task PostResourceTypeSearch(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostResourceTypeSearch <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostResourceTypeSearch <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        // sanity check
        if (Request == null)
        {
            await LogAndReturnError(Response, 500, $"PostResourceTypeSearch <<< cannot process a POST search without a Request");
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();
                string queryString = Request.QueryString.ToString();

                if (!string.IsNullOrEmpty(queryString))
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        content = queryString;
                    }
                    else
                    {
                        content += $"&{queryString}";
                    }
                }

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = queryString,
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    SerializeSummaryFlag = summary ?? string.Empty,
                    Interaction = Common.StoreInteractionCodes.TypeSearch,
                    ResourceType = resourceName,
                    SourceContent = content,
                    SourceFormat = Request.ContentType ?? string.Empty,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                HttpStatusCode sc = _fhirStoreManager[storeName].TypeSearch(
                    ctx,
                    out string resource,
                    out string outcome);

                Response.ContentType = ctx.DestinationFormat;;
                Response.StatusCode = (int)sc;

                await AddBody(Response, null, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostResourceTypeSearch <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostResourceTypeSearch <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a type operation.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="opName">      Name of the operation.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostTypeOperation(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_summary")] string? summary,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostTypeOperation <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostTypeOperation <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        HttpStatusCode sc;
        string resource, outcome;

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    SerializeSummaryFlag = summary ?? string.Empty,
                    Interaction = Common.StoreInteractionCodes.TypeOperation,
                    ResourceType = resourceName,
                    OperationName = "$" + opName,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                // operation
                sc = store.TypeOperation(
                    ctx,
                    out resource,
                    out outcome);

                Response.ContentType = ctx.DestinationFormat; ;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostTypeOperation <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostTypeOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a system search.</summary>
    /// <param name="storeName">  The store.</param>
    /// <param name="format"> Describes the format to use.</param>
    /// <param name="pretty"> The pretty.</param>
    /// <param name="summary">The summary.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/_search")]
    [Consumes("application/x-www-form-urlencoded")]
    public async Task PostSystemSearch(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostSystemSearch <<< no tenant at {storeName}!");
            return;
        }

        // sanity check
        if (Request == null)
        {
            _logger.LogWarning("PostSystemSearch <<< cannot process a POST search without a Request!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();
                string queryString = Request.QueryString.ToString();

                if (!string.IsNullOrEmpty(queryString))
                {
                    if (string.IsNullOrEmpty(content))
                    {
                        content = queryString;
                    }
                    else
                    {
                        content += $"&{queryString}";
                    }
                }

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = queryString,
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    SerializeSummaryFlag = summary ?? string.Empty,
                    Interaction = Common.StoreInteractionCodes.SystemSearch,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                HttpStatusCode sc = store.SystemSearch(
                    ctx,
                    out string resource,
                    out string outcome);

                Response.ContentType = ctx.DestinationFormat;;
                Response.StatusCode = (int)sc;

                await AddBody(Response, null, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostSystemSearch <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostSystemSearch <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a system operation.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="opName">    Name of the operation.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/${opName}")]
    //[Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostSystemOperation(
        [FromRoute] string storeName,
        [FromRoute] string opName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostSystemOperation <<< no tenant at {storeName}!");
            return;
        }

        // sanity check
        if (Request == null)
        {
            _logger.LogWarning("PostSystemOperation <<< cannot process an operation POST without a Request!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Interaction = Common.StoreInteractionCodes.SystemOperation,
                    OperationName = "$" + opName,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                // re-add the prefix $ character since it was stripped during routing

                HttpStatusCode sc = store.SystemOperation(
                        ctx,
                        out string resource,
                        out string outcome);
 
                Response.ContentType = ctx.DestinationFormat;;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostSystemOperation <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostSystemOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts a resource type.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="ifNoneExist"> if none exist.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}/{resourceName}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostResourceType(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "If-None-Exist")] string? ifNoneExist,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostResourceType <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PostResourceType <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            _logger.LogWarning("PostResourceType <<< cannot process a POST without data!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Interaction = Common.StoreInteractionCodes.TypeCreate,
                    ResourceType = resourceName,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                    IfNoneExist = ifNoneExist ?? string.Empty,
                    AllowExistingId = true,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                HttpStatusCode sc;
                string resource, outcome;

                sc = store.InstanceCreate(
                    ctx,
                    out resource,
                    out outcome,
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

                Response.ContentType = ctx.DestinationFormat;;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostResourceType <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostResourceType <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP PUT requests) puts resource instance.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="ifMatch">     A match specifying if.</param>
    /// <param name="ifNoneMatch"> A match specifying if none.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPut, Route("{storeName}/{resourceName}/{id}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PutResourceInstance(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PutResourceInstance <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"PutResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            _logger.LogWarning("PutResourceInstance <<< cannot process a PUT without data!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Interaction = Common.StoreInteractionCodes.InstanceUpdate,
                    ResourceType = resourceName,
                    Id = id,
                    IfMatch = ifMatch ?? string.Empty,
                    IfNoneMatch = ifNoneMatch ?? string.Empty,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                    AllowCreateAsUpdate = true,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                HttpStatusCode sc = store.InstanceUpdate(
                    ctx,
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

                Response.ContentType = ctx.DestinationFormat;;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PutResourceInstance <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PutResourceInstance <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>
    /// (An Action that handles HTTP DELETE requests) deletes the resource instance.
    /// </summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="id">          The identifier.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="ifMatch">     A match specifying if.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpDelete, Route("{storeName}/{resourceName}/{id}")]
    public async Task DeleteResourceInstance(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromRoute] string id,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceInstance <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.InstanceDelete,
            ResourceType = resourceName,
            Id = id,
            IfMatch = ifMatch ?? string.Empty,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc = store.InstanceDelete(
            ctx,
            out string resource,
            out string outcome);

        Response.ContentType = ctx.DestinationFormat;;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
    }

    /// <summary>(An Action that handles HTTP GET requests) gets resource type search.</summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}/{resourceName}")]
    public async Task GetResourceTypeSearch(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"GetResourceTypeSearch <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"GetResourceTypeSearch <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.TypeSearch,
            ResourceType = resourceName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc = store.TypeSearch(
            ctx,
            out string resource,
            out string outcome);

        Response.ContentType = ctx.DestinationFormat;;
        Response.StatusCode = (int)sc;

        await AddBody(Response, null, resource, outcome);
    }

    /// <summary>
    /// (An Action that handles HTTP DELETE requests) deletes a resource based on type search.
    /// </summary>
    /// <param name="storeName">   The store.</param>
    /// <param name="resourceName">Name of the resource.</param>
    /// <param name="format">      Describes the format to use.</param>
    /// <param name="pretty">      The pretty.</param>
    /// <param name="summary">     The summary.</param>
    /// <param name="prefer">      The prefer.</param>
    /// <param name="authHeader">  The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpDelete, Route("{storeName}/{resourceName}")]
    public async Task DeleteResourceConditional(
        [FromRoute] string storeName,
        [FromRoute] string resourceName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceConditional <<< no tenant at {storeName}!");
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            await LogAndReturnError(Response, 404, $"DeleteResourceConditional <<< tenant {storeName} does not support resource {resourceName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.TypeDeleteConditional,
            ResourceType = resourceName,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        // sanity check
        if (Request == null)
        {
            System.Console.WriteLine("DeleteResourceConditional <<< cannot process a conditional delete without a Request!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            string queryString = Request.QueryString.ToString();

            HttpStatusCode sc = store.TypeDelete(
                ctx,
                out string resource,
                out string outcome);

            Response.ContentType = ctx.DestinationFormat;;
            Response.StatusCode = (int)sc;

            await AddBody(Response, prefer, resource, outcome);
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"DeleteResourceConditional <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"DeleteResourceConditional <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP POST requests) posts to the server root.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpPost, Route("{storeName}")]
    [Consumes("application/fhir+json", new[] { "application/fhir+xml", "application/json", "application/xml" })]
    public async Task PostSystemBundle(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"PostSystemBundle <<< no tenant at {storeName}!");
            return;
        }

        // sanity check
        if ((Request == null) || (Request.Body == null))
        {
            _logger.LogWarning("PostSystemBundle <<< cannot process a bundle POST without data!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                FhirRequestContext ctx = new()
                {
                    TenantName = storeName,
                    Store = store,
                    HttpMethod = Request.Method.ToUpperInvariant(),
                    Url = Request.GetDisplayUrl(),
                    UrlPath = Request.Path,
                    UrlQuery = Request.QueryString.ToString(),
                    RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
                    DestinationFormat = GetMimeType(format, Request),
                    SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Interaction = Common.StoreInteractionCodes.SystemBundle,
                    SourceFormat = Request.ContentType ?? string.Empty,
                    SourceContent = content,
                };

                if (!_smartAuthManager.IsAuthorized(ctx))
                {
                    Response.StatusCode = 401;
                    return;
                }

                HttpStatusCode sc = store.ProcessBundle(
                    ctx,
                    out string resource,
                    out string outcome);

                Response.ContentType = ctx.DestinationFormat;;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"PostSystemBundle <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"PostSystemBundle <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }

    /// <summary>(An Action that handles HTTP GET requests) gets system search.</summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="summary">   The summary.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpGet, Route("{storeName}")]
    public async Task GetSystemSearch(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"GetSystemSearch <<< no tenant at {storeName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.SystemSearch,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        HttpStatusCode sc = store.SystemSearch(
            ctx,
            out string resource,
            out string outcome);

        Response.ContentType = ctx.DestinationFormat;;
        Response.StatusCode = (int)sc;

        await AddBody(Response, null, resource, outcome);
    }

    /// <summary>
    /// (An Action that handles HTTP DELETE requests) deletes the system conditional.
    /// </summary>
    /// <param name="storeName"> The store.</param>
    /// <param name="format">    Describes the format to use.</param>
    /// <param name="pretty">    The pretty.</param>
    /// <param name="summary">   The summary.</param>
    /// <param name="prefer">    The prefer.</param>
    /// <param name="authHeader">The authentication header.</param>
    /// <returns>An asynchronous result.</returns>
    [HttpDelete, Route("{storeName}")]
    public async Task DeleteSystemConditional(
        [FromRoute] string storeName,
        [FromQuery(Name = "_format")] string? format,
        [FromQuery(Name = "_pretty")] string? pretty,
        [FromQuery(Name = "_summary")] string? summary,
        [FromHeader(Name = "Prefer")] string? prefer,
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            await LogAndReturnError(Response, 404, $"DeleteSystemConditional <<< no tenant at {storeName}!");
            return;
        }

        FhirRequestContext ctx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = Request.Method.ToUpperInvariant(),
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            SerializeSummaryFlag = summary ?? string.Empty,
            Interaction = Common.StoreInteractionCodes.SystemDeleteConditional,
        };

        if (!_smartAuthManager.IsAuthorized(ctx))
        {
            Response.StatusCode = 401;
            return;
        }

        // sanity check
        if (Request == null)
        {
            _logger.LogWarning("DeleteSystemConditional <<< cannot process a conditional delete without a Request!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            string queryString = Request.QueryString.ToString();

            HttpStatusCode sc = store.SystemDelete(
                ctx,
                out string resource,
                out string outcome);

            Response.ContentType = ctx.DestinationFormat;;
            Response.StatusCode = (int)sc;

            await AddBody(Response, prefer, resource, outcome);
        }
        catch (Exception ex)
        {
            string msg;
            if (ex.InnerException == null)
            {
                msg = $"DeleteSystemConditional <<< caught: {ex.Message}";
            }
            else
            {
                msg = $"DeleteSystemConditional <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
            }

            await LogAndReturnError(Response, 500, msg);
            return;
        }
    }
}
