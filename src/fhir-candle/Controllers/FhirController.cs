// <copyright file="FhirController.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Net;
using System.Security.Cryptography.X509Certificates;
using fhir.candle.Services;
using Fhir.Metrics;
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
            _logger.LogWarning($"GetSmartWellKnown <<< no SMART config for {storeName}!");
            Response.StatusCode = 404;
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
            _logger.LogWarning($"GetMetadata <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);
        //string format = GetMimeType(string.Empty, HttpContext.Request);

        HttpStatusCode sc = _fhirStoreManager[storeName].GetMetadata(
            auth,
            format,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
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
            _logger.LogWarning($"GetTypeOperation <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"GetTypeOperation <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request, true);

        HttpStatusCode sc = store.TypeOperation(
            auth,
            resourceName,
            "$" + opName,
            Request.QueryString.ToString(),
            string.Empty,
            string.Empty,
            format,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            out string resource,
            out string outcome);

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
    }

    /// <summary>Query if this object is authorized.</summary>
    /// <param name="storeName">    Name of the store.</param>
    /// <param name="store">        The store.</param>
    /// <param name="requestMethod">The request method.</param>
    /// <param name="requestUrl">   URL of the request.</param>
    /// <param name="authHeader">   The authentication header.</param>
    /// <param name="statusCode">   [out] The status code.</param>
    /// <returns>True if authorized, false if not.</returns>
    private bool IsAuthorized(
        string storeName,
        IFhirStore store,
        string requestMethod,
        string requestUrl,
        string? authHeader,
        out int statusCode,
        out AuthorizationInfo? auth)
    {
        if (string.IsNullOrEmpty(authHeader))
        {
            auth = null;

            if (store.Config.SmartRequired)
            {
                _logger.LogWarning($"IsAuthorized <<< tenant {store} requires authorization!");
                statusCode = 401;
                return false;
            }

            statusCode = 200;
            return true;
        }

        string[] authComponents = authHeader.Split(' ');

        if (authComponents.Length < 2)
        {
            _logger.LogWarning($"IsAuthorized <<< malformed authorization header: {authHeader}!");
            statusCode = 400;
            auth = null;
            return false;
        }

        Common.StoreInteractionCodes? interaction = store.DetermineInteraction(
            requestMethod,
            requestUrl,
            out _,
            out _,
            out _,
            out string requestResourceType,
            out _,
            out string requestOperationName,
            out string requestCompartmentType,
            out _);

        if (interaction == null)
        {
            _logger.LogError($"IsAuthorized <<< could not parse request {Request.GetDisplayUrl()}!");
            statusCode = 500;
            auth = null;
            return false;
        }

        bool authorized = _smartAuthManager.IsAuthorized(
            storeName,
            authComponents[1],
            Request.Method?.ToString() ?? string.Empty,
            (Common.StoreInteractionCodes)interaction,
            requestResourceType,
            requestOperationName,
            requestCompartmentType,
            out auth);

        if (authorized)
        {
            statusCode = 200;
            return true;
        }

        statusCode = 401;
        return false;
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
            _logger.LogWarning($"GetResourceInstance <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"GetResourceInstance <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc = store.InstanceRead(
            auth,
            resourceName,
            id,
            format,
            summary ?? string.Empty,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            ifMatch ?? string.Empty,
            ifModifiedSince ?? string.Empty,
            ifNoneMatch ?? string.Empty,
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
            _logger.LogWarning($"GetInstanceOperation <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"GetInstanceOperation <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request, true);

        HttpStatusCode sc;
        string resource, outcome;

        // operation
        sc = store.InstanceOperation(
            auth,
            resourceName,
            "$" + opName,
            id,
            Request.QueryString.ToString(),
            string.Empty,
            string.Empty,
            format,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            out resource,
            out outcome);

        Response.ContentType = format;
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
            _logger.LogWarning($"PostInstanceOperation <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"PostInstanceOperation <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "POST", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc;
        string resource, outcome;

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                // operation
                sc = store.InstanceOperation(
                    auth,
                    resourceName,
                    "$" + opName,
                    id,
                    Request.QueryString.ToString(),
                    content,
                    Request.ContentType ?? string.Empty,
                    format,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    out resource,
                    out outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostInstanceOperation <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostInstanceOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
            return;
        }

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
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
            _logger.LogWarning($"PostResourceTypeSearch <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"PostResourceTypeSearch <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "POST", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        // sanity check
        if (Request == null)
        {
            _logger.LogWarning("PostResourceTypeSearch <<< cannot process a POST search without a Request!");
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

                HttpStatusCode sc = _fhirStoreManager[storeName].TypeSearch(
                    auth,
                    resourceName,
                    content,
                    format,
                    summary ?? string.Empty,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    out string resource,
                    out string outcome);

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, null, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostResourceTypeSearch <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostResourceTypeSearch <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
            _logger.LogWarning($"PostTypeOperation <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"PostTypeOperation <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "POST", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc;
        string resource, outcome;

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                // operation
                sc = store.TypeOperation(
                    auth,
                    resourceName,
                    "$" + opName,
                    Request.QueryString.ToString(),
                    content,
                    Request.ContentType ?? string.Empty,
                    format,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    out resource,
                    out outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostTypeOperation <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostTypeOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
            return;
        }

        Response.ContentType = format;
        Response.StatusCode = (int)sc;

        await AddBody(Response, prefer, resource, outcome);
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
            _logger.LogWarning($"PostSystemSearch <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "POST", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

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

                HttpStatusCode sc = store.SystemSearch(
                    auth,
                    content,
                    format,
                    summary ?? string.Empty,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    out string resource,
                    out string outcome);

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, null, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostSystemSearch <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostSystemSearch <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
            _logger.LogWarning($"PostSystemOperation <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, HttpContext.Request, true);

        // sanity check
        if (Request == null)
        {
            _logger.LogWarning("PostResourceType <<< cannot process an operation POST without a Request!");
            Response.StatusCode = 400;
            return;
        }

        try
        {
            // read the post body to process
            using (StreamReader reader = new StreamReader(Request.Body))
            {
                string content = await reader.ReadToEndAsync();

                // re-add the prefix $ character since it was stripped during routing

                HttpStatusCode sc = store.SystemOperation(
                        auth,
                        "$" + opName,
                        Request.QueryString.ToString(),
                        content,
                        Request.ContentType ?? string.Empty,
                        format,
                        pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                        out string resource,
                        out string outcome);
 
                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostSystemOperation <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostSystemOperation <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
            _logger.LogWarning($"PostResourceType <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"PostResourceType <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "POST", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

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

                HttpStatusCode sc;
                string resource, outcome;

                sc = store.InstanceCreate(
                    auth,
                    resourceName,
                    content,
                    Request.ContentType ?? string.Empty,
                    format,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    ifNoneExist ?? string.Empty,
                    true,
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

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostResourceType <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostResourceType <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
            _logger.LogWarning($"PutResourceInstance <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"PutResourceInstance <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "PUT", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

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

                HttpStatusCode sc = store.InstanceUpdate(
                    auth,
                    resourceName,
                    id,
                    content,
                    HttpContext.Request.ContentType ?? string.Empty,
                    format,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    Request.QueryString.ToString(),
                    ifMatch ?? string.Empty,
                    ifNoneMatch ?? string.Empty,
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
            if (ex.InnerException == null)
            {
                _logger.LogError($"PutResourceInstance <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PutResourceInstance <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
        [FromHeader(Name = "Authorization")] string? authHeader)
    {
        if ((!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store)) ||
            (store == null))
        {
            _logger.LogWarning($"DeleteResourceInstance <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"DeleteResourceInstance <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "DELETE", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc = store.InstanceDelete(
            auth,
            resourceName,
            id,
            format,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            string.Empty,
            out string resource,
            out string outcome);

        Response.ContentType = format;
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
            _logger.LogWarning($"GetResourceTypeSearch <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"GetResourceTypeSearch <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc = store.TypeSearch(
            auth,
            resourceName,
            Request.QueryString.ToString(),
            format,
            summary ?? string.Empty,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            out string resource,
            out string outcome);

        Response.ContentType = format;
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
            _logger.LogWarning($"DeleteResourceConditional <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!store.SupportsResource(resourceName))
        {
            _logger.LogWarning($"DeleteResourceConditional <<< tenant {storeName} does not support {resourceName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "DELETE", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

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
                auth,
                resourceName,
                queryString,
                format,
                pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                out string resource,
                out string outcome);

            Response.ContentType = format;
            Response.StatusCode = (int)sc;

            await AddBody(Response, prefer, resource, outcome);
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"DeleteResourceConditional <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"DeleteResourceConditional <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
            _logger.LogWarning($"PostSystemBundle <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "POST", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, HttpContext.Request);

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

                HttpStatusCode sc = store.ProcessBundle(
                    auth,
                    content,
                    Request.ContentType ?? string.Empty,
                    format,
                    pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                    out string resource,
                    out string outcome);

                Response.ContentType = format;
                Response.StatusCode = (int)sc;

                await AddBody(Response, prefer, resource, outcome);
            }
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"PostSystemBundle <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"PostSystemBundle <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
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
            _logger.LogWarning($"GetSystemSearch <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "GET", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

        HttpStatusCode sc = store.SystemSearch(
            auth,
            Request.QueryString.ToString(),
            format,
            summary ?? string.Empty,
            pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            out string resource,
            out string outcome);

        Response.ContentType = format;
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
            _logger.LogWarning($"DeleteSystemConditional <<< no tenant at {storeName}!");
            Response.StatusCode = 404;
            return;
        }

        if (!IsAuthorized(storeName, store, "DELETE", Request.GetDisplayUrl(), authHeader, out int authStatus, out AuthorizationInfo? auth))
        {
            Response.StatusCode = authStatus;
            return;
        }

        format = GetMimeType(format, Request);

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
                auth,
                queryString,
                format,
                pretty?.Equals("true", StringComparison.Ordinal) ?? false,
                out string resource,
                out string outcome);

            Response.ContentType = format;
            Response.StatusCode = (int)sc;

            await AddBody(Response, prefer, resource, outcome);
        }
        catch (Exception ex)
        {
            if (ex.InnerException == null)
            {
                _logger.LogError($"DeleteSystemConditional <<< caught: {ex.Message}");
            }
            else
            {
                _logger.LogError($"DeleteSystemConditional <<< caught: {ex.Message}, inner: {ex.InnerException.Message}");
            }

            Response.StatusCode = 500;
            return;
        }
    }
}
