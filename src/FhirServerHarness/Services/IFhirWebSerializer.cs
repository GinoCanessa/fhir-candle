// <copyright file="IFhirWebSerializer.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Common.Models;
using System.Net;

namespace FhirStore.Common.Services;

/// <summary>Interface for FHIR web serializer.</summary>
public interface IFhirWebSerializer : IHostedService, IDisposable
{
    /// <summary>Values that represent return Preferences.</summary>
    public enum ReturnPrefCodes
    {
        /// <summary>An enum constant representing the minimal option.</summary>
        Minimal,

        /// <summary>An enum constant representing the operation outcome option.</summary>
        OperationOutcome,

        /// <summary>An enum constant representing the representation option.</summary>
        Representation,
    }
    
    /// <summary>Values that represent serialization format codes.</summary>
    public enum SerializationFormatCodes
    {
        /// <summary>An enum constant representing the JSON option.</summary>
        Json,

        /// <summary>An enum constant representing the XML option.</summary>
        Xml,
    }

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
    System.Threading.Tasks.Task Serialize(
        Uri baseUri,
        HttpContext context,
        Hl7.Fhir.Model.Resource resource,
        SerializationFormatCodes serializationFormat = SerializationFormatCodes.Json,
        int statusCode = 200,
        string location = "",
        ReturnPrefCodes preferredResponse = ReturnPrefCodes.Representation,
        string failureContent = "");
}
