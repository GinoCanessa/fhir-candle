// <copyright file="OpPasClaimSubmit.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FhirCandle.Operations;

/// <summary>
/// This operation is used to submit a Pre-Authorization Claim Request for adjudication as a
/// Bundle containing the PASClaimRequest and other referenced resources for processing.
/// </summary>
public class OpPasClaimSubmit : IFhirOperation
{
    /// <summary>Gets the name of the operation.</summary>
    public string OperationName => "$submit";

    /// <summary>Gets the operation version.</summary>
    public string OperationVersion => "1.2.0";

    /// <summary>Gets the canonical by FHIR version.</summary>
    public Dictionary<FhirCandle.Models.TenantConfiguration.SupportedFhirVersions, string> CanonicalByFhirVersion => new()
    {
        { FhirCandle.Models.TenantConfiguration.SupportedFhirVersions.R4, "http://hl7.org/fhir/us/davinci-pas/OperationDefinition/Claim-submit" },
    };

    /// <summary>Gets a value indicating whether this operation is a named query.</summary>
    public bool IsNamedQuery => false;

    /// <summary>Gets a value indicating whether we allow get.</summary>
    public bool AllowGet => false;

    /// <summary>Gets a value indicating whether we allow post.</summary>
    public bool AllowPost => true;

    /// <summary>Gets a value indicating whether we allow system level.</summary>
    public bool AllowSystemLevel => false;

    /// <summary>Gets a value indicating whether we allow resource level.</summary>
    public bool AllowResourceLevel => true;

    /// <summary>Gets a value indicating whether we allow instance level.</summary>
    public bool AllowInstanceLevel => false;

    /// <summary>Gets a value indicating whether the accepts non FHIR.</summary>
    public bool AcceptsNonFhir => true;

    /// <summary>Gets a value indicating whether the returns non FHIR.</summary>
    public bool ReturnsNonFhir => false;

    /// <summary>
    /// If this operation requires a specific FHIR package to be loaded, the package identifier.
    /// </summary>
    public string RequiresPackage => "hl7.fhir.us.davinci-pas";

    /// <summary>Gets the supported resources.</summary>
    public HashSet<string> SupportedResources => new()
    {
        "Claim"
    };

    /// <summary>Executes the Subscription/$events operation.</summary>
    /// <param name="store">           The store.</param>
    /// <param name="resourceType">    Type of the resource.</param>
    /// <param name="resourceStore">   The resource store.</param>
    /// <param name="instanceId">      Identifier for the instance.</param>
    /// <param name="focusResource">   The focus resource.</param>
    /// <param name="queryString">     The query string.</param>
    /// <param name="bodyResource">    The body resource.</param>
    /// <param name="bodyContent">     The original body content.</param>
    /// <param name="contentType">     Type of the content.</param>
    /// <param name="responseResource">[out] The response resource.</param>
    /// <param name="responseOutcome"> [out] The response outcome.</param>
    /// <param name="contentLocation"> [out] The content location.</param>
    /// <returns>A HttpStatusCode.</returns>
    public HttpStatusCode DoOperation(
        Storage.VersionedFhirStore store,
        string resourceType,
        Storage.IVersionedResourceStore? resourceStore,
        string instanceId,
        Hl7.Fhir.Model.Resource? focusResource,
        string queryString,
        Hl7.Fhir.Model.Resource? bodyResource,
        string bodyContent,
        string contentType,
        out Hl7.Fhir.Model.Resource? responseResource,
        out Hl7.Fhir.Model.OperationOutcome? responseOutcome,
        out string contentLocation)
    {
        if ((bodyResource == null) ||
            (bodyResource is not Bundle b))
        {
            responseResource = null;
            responseOutcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Structure,
                        Diagnostics = "PAS Claim Submit requires a PASRequestBundle as input.",
                    },
                },
            };
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        if (b.Type != Bundle.BundleType.Collection)
        {
            responseResource = null;
            responseOutcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Structure,
                        Diagnostics = "PAS Claim Submit PASRequestBundle SHALL be a `collection`.",
                    },
                },
            };
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        IEnumerable<Resource> claims = b.Entry.Select(e => e.Resource).Where(r => r is Claim);

        if (!claims.Any())
        {
            responseResource = null;
            responseOutcome = new OperationOutcome()
            {
                Id = Guid.NewGuid().ToString(),
                Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Fatal,
                        Code = OperationOutcome.IssueType.Structure,
                        Diagnostics = "Submitted bundle does not contain any Claim resources.",
                    },
                },
            };
            contentLocation = string.Empty;

            return HttpStatusCode.UnprocessableEntity;
        }

        responseOutcome = new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>(),
        };


        Bundle response = new()
        {
            Id = Guid.NewGuid().ToString(),
            Meta = new()
            {
                LastUpdated = DateTimeOffset.UtcNow,
            },
            Type = Bundle.BundleType.Collection,
            Timestamp = DateTimeOffset.UtcNow,
            Entry = new List<Bundle.EntryComponent>(),
        };

        foreach (Resource entry in b.Entry.Select(e => e.Resource).Where(r => r != null))
        {
            if (entry is Claim c)
            {
                if (!c.Identifier.Any())
                {
                    responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Required,
                        Diagnostics = $"Claim {c.Id} is missing mandatory `identifier` element.",
                    });

                    continue;
                }

                if (c.Provider == null)
                {
                    responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Required,
                        Diagnostics = $"Claim {c.Id} is missing mandatory `provider` element.",
                    });

                    continue;
                }

                if (!c.Insurance.Any())
                {
                    responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Required,
                        Diagnostics = $"Claim {c.Id} is missing mandatory `insurance` element.",
                    });

                    continue;
                }

                if (!c.Item.Any())
                {
                    responseOutcome.Issue.Add(new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Error,
                        Code = OperationOutcome.IssueType.Required,
                        Diagnostics = $"Claim {c.Id} is missing mandatory `item` element.",
                    });

                    continue;
                }
            }

            HttpStatusCode sc = store.DoInstanceCreate(
                entry.TypeName,
                entry,
                string.Empty,
                true,
                out Resource? r,
                out OperationOutcome subOutcome,
                out string eTag,
                out string lastModified,
                out string location);

            response.Entry.Add(new Bundle.EntryComponent()
            {
                FullUrl = location,
                Resource = r,
                Response = new Bundle.ResponseComponent()
                {
                    Status = sc.ToString(),
                    Outcome = subOutcome,
                    Etag = eTag,
                    LastModified = Models.ParsedSearchParameter.TryParseDateString(lastModified, out DateTimeOffset dto, out _)
                        ? dto
                        : null,
                    Location = location,
                },
            });
        }

        responseResource = response;
        responseOutcome = responseOutcome ?? new OperationOutcome()
        {
            Id = Guid.NewGuid().ToString(),
            Issue = new List<OperationOutcome.IssueComponent>()
                {
                    new OperationOutcome.IssueComponent()
                    {
                        Severity = OperationOutcome.IssueSeverity.Success,
                        Code = OperationOutcome.IssueType.Success,
                        Diagnostics = "See response bundle for details.",
                    },
                },
        };
        contentLocation = string.Empty;

        return HttpStatusCode.OK;
    }


    /// <summary>Gets an OperationDefinition for this operation.</summary>
    /// <param name="fhirVersion">The FHIR version.</param>
    /// <returns>The definition.</returns>
    public Hl7.Fhir.Model.OperationDefinition? GetDefinition(
        FhirCandle.Models.TenantConfiguration.SupportedFhirVersions fhirVersion)
    {
        // operation has canonical definition in package
        return null;
    }
}

