// <copyright file="FhirStoreTestsR4B.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4B;

using FhirCandle.Models;
using FhirCandle.Storage;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using candleR4B::FhirCandle.Models;
using candleR4B::FhirCandle.Storage;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore R4 functionality.</summary>
public class FhirStoreTestsR4B: IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly TenantConfiguration _config = new()
    {
        FhirVersion = TenantConfiguration.SupportedFhirVersions.R4B,
        ControllerName = "r4b",
        BaseUrl = "http://localhost/fhir/r4b",
    };

    private const int _expectedRestResources = 140;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR4B(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    public void Dispose()
    {
        // cleanup
    }

    [Theory]
    [FileData("data/r4b/searchparameter-patient-multiplebirth.json")]
    public void ResourceCreateSearchParameter(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
            AllowCreateAsUpdate = true,
            AllowExistingId = true,
        };

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            ctx,
            out string serializedResource,
            out string serializedOutcome,
            out string eTag,
            out string lastModified,
            out string location);

        scCreate.Should().Be(HttpStatusCode.Created);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().Contain("SearchParameter/");

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter/Patient-multiplebirth",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        HttpStatusCode scRead = fhirStore.InstanceRead(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().EndWith("SearchParameter/Patient-multiplebirth");
        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/searchparameter-patient-multiplebirth.json")]
    public void CreateSearchParameterCapabilityCount(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        FhirRequestContext ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/metadata",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        // read the metadata
        HttpStatusCode scRead = fhirStore.GetMetadata(
            ctx,
            out string serializedResource,
            out string serializedOutcome,
            out string eTag,
            out string lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();

        MinimalCapabilities? capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(serializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        MinimalCapabilities.MinimalRest rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();

        int spCount = 0;
        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            spCount = r.SearchParams?.Count() ?? 0;
            break;
        }

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "POST",
            Url = fhirStore.Config.BaseUrl + "/SearchParameter",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            SourceContent = json,
            DestinationFormat = "application/fhir+json",
            AllowExistingId = true,
            AllowCreateAsUpdate = true,
        };

        // add a search parameter for the patient resource
        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out string location);

        ctx = new()
        {
            TenantName = fhirStore.Config.ControllerName,
            Store = fhirStore,
            HttpMethod = "GET",
            Url = fhirStore.Config.BaseUrl + "/metadata",
            Authorization = null,
            SourceFormat = "application/fhir+json",
            DestinationFormat = "application/fhir+json",
        };

        // read the metadata again
        scRead = fhirStore.GetMetadata(
            ctx,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();

        capabilities = JsonSerializer.Deserialize<MinimalCapabilities>(serializedResource);

        capabilities.Should().NotBeNull();
        capabilities!.Rest.Should().NotBeNullOrEmpty();

        rest = capabilities!.Rest!.First();
        rest.Mode.Should().Be("server");
        rest.Resources.Should().NotBeNullOrEmpty();

        foreach (MinimalCapabilities.MinimalResource r in rest.Resources!)
        {
            if (r.ResourceType != "Patient")
            {
                continue;
            }

            (r.SearchParams?.Count() ?? 0).Should().Be(spCount + 1);
            break;
        }
    }
}