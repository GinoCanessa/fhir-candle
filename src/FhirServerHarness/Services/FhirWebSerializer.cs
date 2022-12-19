// <copyright file="FhirWebSerializer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using System.Net;
using static FhirServerHarness.Services.IFhirWebSerializer;

namespace FhirServerHarness.Services;

/// <summary>An object for persisting FHIR web data.</summary>
public class FhirWebSerializer : IFhirWebSerializer
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    private Hl7.Fhir.Serialization.FhirJsonSerializer _jsonSerializer = new(new Hl7.Fhir.Serialization.SerializerSettings()
    {
        AppendNewLine = false,
        Pretty = false,
    });

    private Hl7.Fhir.Serialization.FhirXmlSerializer _xmlSerializer = new(new Hl7.Fhir.Serialization.SerializerSettings()
    {
        AppendNewLine = false,
        Pretty = false,
    });

    /// <summary>Serialize this object to the given stream.</summary>
    /// <param name="baseUri">            URI of the base.</param>
    /// <param name="context">            The context.</param>
    /// <param name="resource">           The resource.</param>
    /// <param name="serializationFormat">(Optional) The serialization format.</param>
    /// <param name="statusCode">         (Optional) The status code.</param>
    /// <param name="location">           (Optional) The location.</param>
    /// <param name="preferredResponse">  (Optional) The preferred response.</param>
    /// <param name="failureContent">     (Optional) The failure content.</param>
    /// <returns>A System.Threading.Tasks.Task.</returns>
    public async System.Threading.Tasks.Task Serialize(
        Uri baseUri,
        HttpContext context,
        Resource resource,
        SerializationFormatCodes serializationFormat = SerializationFormatCodes.Json,
        int statusCode = 200, 
        string location = "",
        ReturnPrefCodes preferredResponse = ReturnPrefCodes.Representation,
        string failureContent = "")
    {
        switch (resource.TypeName)
        {
            case "OperationOutcome":
            case "Bundle":
            case "Parameters":
                if (!string.IsNullOrEmpty(location))
                {
                    context.Response.Headers.Add("Location", location);
                }

                break;

            default:
                if (string.IsNullOrEmpty(location))
                {
                    context.Response.Headers.Add(
                        "Location", 
                        new Uri(baseUri, new Uri($"{resource.TypeName}/{resource.Id}", UriKind.Relative)).ToString());
                }
                else
                {
                    context.Response.Headers.Add("Location", location);
                }

                break;
        }

        context.Response.Headers.Add("Access-Control-Expose-Headers", "Location,ETag");

        switch (preferredResponse)
        {
            case ReturnPrefCodes.Minimal:
                context.Response.ContentType = "text/plain";
                context.Response.StatusCode = statusCode;
                break;

            case ReturnPrefCodes.OperationOutcome:
                OperationOutcome outcome = BuildOutcomeForRequest(statusCode);
                await WriteResponse(context, outcome, statusCode, serializationFormat);
                break;

            case ReturnPrefCodes.Representation:
            default:
                await WriteResponse(context, resource, statusCode, serializationFormat);
                break;
        }

        _ = context.Response.CompleteAsync();
    }

    /// <summary>Builds outcome for request.</summary>
    /// <param name="statusCode">The status code.</param>
    /// <returns>An OperationOutcome.</returns>
    private OperationOutcome BuildOutcomeForRequest(
        int statusCode)
    {
        HttpStatusCode sc = (HttpStatusCode)statusCode;

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
                    Diagnostics = $"Request failed with status code {statusCode}",
                },
            },
        };
    }

    /// <summary>Writes a response.</summary>
    /// <param name="context">            The context.</param>
    /// <param name="resource">           The resource.</param>
    /// <param name="statusCode">         The status code.</param>
    /// <param name="serializationFormat">The serialization format.</param>
    /// <returns>A System.Threading.Tasks.Task.</returns>
    private async System.Threading.Tasks.Task WriteResponse(
        HttpContext context,
        Resource resource,
        int statusCode,
        SerializationFormatCodes serializationFormat)
    {
        switch (serializationFormat)
        {
            case SerializationFormatCodes.Json:
                context.Response.ContentType = "application/fhir+json";
                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsync(_jsonSerializer.SerializeToString(resource));
                break;

            case SerializationFormatCodes.Xml:
                context.Response.ContentType = "application/fhir+xml";
                context.Response.StatusCode = statusCode;
                await context.Response.WriteAsync(_xmlSerializer.SerializeToString(resource));
                break;

            default:
                break;
        }
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    System.Threading.Tasks.Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    System.Threading.Tasks.Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return System.Threading.Tasks.Task.CompletedTask;
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
