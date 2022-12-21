// <copyright file="FhirStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Serialization;
using Hl7.FhirPath;
using Microsoft.AspNetCore.Http;
using System.Net;

namespace FhirServerHarness.Storage;

/// <summary>A FHIR store.</summary>
public class FhirStore : IFhirStore
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed;

    private static readonly FhirPathCompiler _compiler = new();


    private FhirJsonParser _jsonParser = new(new ParserSettings()
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
    });

    private FhirJsonSerializationSettings _jsonSerializerSettings = new()
    {
        AppendNewLine = false,
        Pretty = false,
        IgnoreUnknownElements = true,
    };

    private FhirXmlParser _xmlParser = new(new ParserSettings()
    {
        AcceptUnknownMembers = true,
        AllowUnrecognizedEnums = true,
    });

    private FhirXmlSerializationSettings _xmlSerializerSettings = new()
    {
        AppendNewLine = false,
        Pretty = false,
        IgnoreUnknownElements = true,
    };


    /// <summary>The store.</summary>
    private Dictionary<string, IResourceStore> _store = new();

    /// <summary>Gets the supported resources.</summary>
    public IEnumerable<string> SupportedResources => _store.Keys.ToArray<string>();

    /// <summary>Initializes this object.</summary>
    /// <param name="config">The configuration.</param>
    public void Init(ProviderConfiguration config)
    {
        Type rsType = typeof(ResourceStore<>);

        foreach ((string tn, Type t) in ModelInfo.FhirTypeToCsType)
        {
            // skip non-resources
            if (!ModelInfo.IsKnownResource(tn))
            {
                continue;
            }

            // skip resources we do not store (per spec)
            switch (tn)
            {
                case "Parameters":
                case "OperationOutcome":
                case "SubscriptionStatus":
                    continue;
            }

            Type[] tArgs = { t };

            IResourceStore? irs = (IResourceStore?)Activator.CreateInstance(rsType.MakeGenericType(tArgs));

            if (irs != null)
            {
                _store.Add(tn, irs);
            }
        }
    }

    /// <summary>Builds outcome for request.</summary>
    /// <param name="sc">     The screen.</param>
    /// <param name="message">(Optional) The message.</param>
    /// <returns>An OperationOutcome.</returns>
    internal OperationOutcome BuildOutcomeForRequest(HttpStatusCode sc, string message = "")
    {
        if (sc.IsSuccessful())
        {
            return new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Information,
                        Code = OperationOutcome.IssueType.Unknown,
                        Diagnostics = $"Request processed successfully",
                    },
                },
            };
        }

        return new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>()
            {
                new OperationOutcome.IssueComponent()
                {
                    Severity = OperationOutcome.IssueSeverity.Error,
                    Code = OperationOutcome.IssueType.Unknown,
                    Diagnostics = $"Request failed with status code {sc.ToString()}",
                },
            },
        };
    }

    /// <summary>Serialize this object to the proper format.</summary>
    /// <param name="instance">   The instance.</param>
    /// <param name="destFormat"> Destination format.</param>
    /// <param name="summaryType">(Optional) Type of the summary.</param>
    /// <returns>A string.</returns>
    private string SerializeFhir(
        Resource instance,
        string destFormat,
        SummaryType summaryType = SummaryType.False)
    {
        // TODO: Need to add back in summary provider
        //if (summaryType == SummaryType.False)
        //{
            switch (destFormat)
            {
                case "xml":
                case "fhir+xml":
                case "application/xml":
                case "application/fhir+xml":
                    return instance.ToXml(_xmlSerializerSettings);

                // default to JSON
                default:
                    return instance.ToJson(_jsonSerializerSettings);
            }
        //}
    }

    /// <summary>Instance create.</summary>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="ifNoneExist">       if none exist.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceCreate(
        string resourceType,
        string content,
        string sourceFormat,
        string destFormat,
        string ifNoneExist,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified)
    {
        object parsed;

        switch (sourceFormat)
        {
            case "json":
            case "fhir+json":
            case "application/json":
            case "application/fhir+json":
                parsed = _jsonParser.Parse(content);
                break;

            case "xml":
            case "fhir+xml":
            case "application/xml":
            case "application/fhir+xml":
                parsed = _xmlParser.Parse(content);
                break;

            default:
                {
                    serializedResource = string.Empty;
                    
                    OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.UnsupportedMediaType, "Unsupported media type");
                    serializedOutcome = SerializeFhir(oo, destFormat);

                    eTag = string.Empty;
                    lastModified = string.Empty;
                    return HttpStatusCode.UnsupportedMediaType;
                }
        }

        if (parsed == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Failed to parse resource content");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        if (parsed is not Resource r)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Data is not a valid FHIR resource");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        if (r.TypeName != resourceType)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {r.TypeName} does not match request: {resourceType}");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        Resource? stored = _store[resourceType].InstanceCreate(r);

        if (stored == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to create resource");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        serializedResource = SerializeFhir(stored, destFormat, SummaryType.False);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.Created, $"Created {stored.TypeName}/{stored.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat);

        eTag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"";
        lastModified = (stored.Meta?.LastUpdated == null) ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        return HttpStatusCode.Created;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceDelete(
        string resourceType,
        string id,
        string destFormat,
        string ifMatch,
        out string serializedResource,
        out string serializedOutcome)
    {
        throw new NotImplementedException();
    }

    /// <summary>Instance read.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="summaryFlag">       The summary flag.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifModifiedSince">   if modified since.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceRead(
        string resourceType,
        string id,
        string destFormat,
        string summaryFlag,
        string ifMatch,
        string ifModifiedSince,
        string ifNoneMatch,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Resource type is required");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.BadRequest;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.BadRequest;
        }

        if (string.IsNullOrEmpty(id))
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "ID required for instance-level read.");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        Resource? stored = _store[resourceType].InstanceRead(id);

        if (stored == null)
        {
            serializedResource = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.InternalServerError, $"Failed to read resource: {resourceType}/{id}");
            serializedOutcome = SerializeFhir(oo, destFormat);

            eTag = string.Empty;
            lastModified = string.Empty;
            return HttpStatusCode.UnsupportedMediaType;
        }

        serializedResource = SerializeFhir(stored, destFormat, SummaryType.False);
        OperationOutcome sucessOutcome = BuildOutcomeForRequest(HttpStatusCode.OK, $"Read {stored.TypeName}/{stored.Id}");
        serializedOutcome = SerializeFhir(sucessOutcome, destFormat);

        eTag = string.IsNullOrEmpty(stored.Meta?.VersionId) ? string.Empty : $"W/\"{stored.Meta.VersionId}\"";
        lastModified = (stored.Meta?.LastUpdated == null) ? string.Empty : stored.Meta.LastUpdated.Value.UtcDateTime.ToString("r");
        return HttpStatusCode.OK;
    }

    /// <summary>Instance update.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                [out] The identifier.</param>
    /// <param name="content">           The content.</param>
    /// <param name="sourceFormat">      Source format.</param>
    /// <param name="destFormat">        Destination format.</param>
    /// <param name="ifMatch">           A match specifying if.</param>
    /// <param name="ifNoneMatch">       A match specifying if none.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode InstanceUpdate(
        string resourceType,
        string id,
        string content,
        string sourceFormat,
        string destFormat,
        string ifMatch,
        string ifNoneMatch,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified)
    {
        throw new NotImplementedException();
    }

    /// <summary>Type search.</summary>
    /// <param name="resourceType">     Type of the resource.</param>
    /// <param name="queryString">      The query string.</param>
    /// <param name="destFormat">       Destination format.</param>
    /// <param name="serializedBundle"> [out] The serialized bundle.</param>
    /// <param name="serializedOutcome">[out] The serialized outcome.</param>
    /// <returns>A HttpStatusCode.</returns>
    HttpStatusCode TypeSearch(
        string resourceType,
        string queryString,
        string destFormat,
        out string serializedBundle,
        out string serializedOutcome)
    {
        if (string.IsNullOrEmpty(resourceType))
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, "Resource type is required");
            serializedOutcome = SerializeFhir(oo, destFormat);

            return HttpStatusCode.BadRequest;
        }

        if (!_store.ContainsKey(resourceType))
        {
            serializedBundle = string.Empty;

            OperationOutcome oo = BuildOutcomeForRequest(HttpStatusCode.BadRequest, $"Resource type: {resourceType} is not supported");
            serializedOutcome = SerializeFhir(oo, destFormat);

            return HttpStatusCode.BadRequest;
        }

        
    }


    /// <summary>
    /// Releases the unmanaged resources used by the
    /// FhirModelComparer.Server.Services.FhirManagerService and optionally releases the managed
    /// resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _hasDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}