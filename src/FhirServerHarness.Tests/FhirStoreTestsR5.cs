// <copyright file="FhirStoreTestsR5.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias storeR5;

using FhirStore.Common.Models;
using FhirStore.Common.Storage;
using FhirServerHarness.Tests.Extensions;
using FhirServerHarness.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using storeR5::FhirStore.Models;
using storeR5::FhirStore.Storage;

namespace FhirServerHarness.Tests;

/// <summary>Unit tests core FhirStore R5 functionality.</summary>
public class FhirStoreTestsR5: IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly ProviderConfiguration _config = new()
    {
        FhirVersion = ProviderConfiguration.SupportedFhirVersions.R5,
        TenantRoute = "r5",
        BaseUrl = "http://localhost:5101/r5",
    };

    private const int _expectedRestResources = 157;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR5(ITestOutputHelper testOutputHelper)
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

    [Fact]
    public void GetMetadata()
    {
        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scRead = fhirStore.GetMetadata(
            "application/fhir+json",
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
        int resourceCount = rest.Resources!.Count();
        resourceCount.Should().Be(_expectedRestResources);
    }

    [Theory]
    [FileData("data/r5/patient-example.json")]
    public void ResourceCreatePatient(string json)
    {
        //_testOutputHelper.WriteLine(json);

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "Patient",
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
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
        location.Should().Be("Patient/example");

        HttpStatusCode scRead = fhirStore.InstanceRead(
            "Patient",
            "example",
            "application/fhir+json",
            string.Empty,
            eTag,
            lastModified,
            string.Empty,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().StartWith("Patient/");
    }

    [Theory]
    [FileData("data/r5/Observation-example.json")]
    public void ResourceCreateObservation(string json)
    {
        //_testOutputHelper.WriteLine(json);

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "Observation",
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
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
        location.Should().Be("Observation/example");

        HttpStatusCode scRead = fhirStore.InstanceRead(
            "Observation",
            "example",
            "application/fhir+json",
            string.Empty,
            eTag,
            lastModified,
            string.Empty,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().StartWith("Observation/");
    }

    [Theory]
    [FileData("data/r5/searchparameter-patient-multiplebirth.json")]
    public void ResourceCreateSearchParameter(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
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
        location.Should().StartWith("SearchParameter/");

        HttpStatusCode scRead = fhirStore.InstanceRead(
            "SearchParameter",
            "Patient-multiplebirth",
            "application/fhir+json",
            string.Empty,
            eTag,
            lastModified,
            string.Empty,
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
    [FileData("data/r5/searchparameter-patient-multiplebirth.json")]
    public void CreateSearchParameterCapabilityCount(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        // read the metadata
        HttpStatusCode scRead = fhirStore.GetMetadata(
            "application/fhir+json",
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

        // add a search parameter for the patient resource
        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out string location);

        // read the metadata again
        scRead = fhirStore.GetMetadata(
            "application/fhir+json",
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

    [Theory]
    [FileData("data/r5/subscriptiontopic-encounter-example.json")]
    public void ResourceCreateSubscriptionTopic(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "SubscriptionTopic",
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
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
        location.Should().StartWith("SubscriptionTopic/");

        HttpStatusCode scRead = fhirStore.InstanceRead(
            "SubscriptionTopic",
            "encounter-example",
            "application/fhir+json",
            string.Empty,
            eTag,
            lastModified,
            string.Empty,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().EndWith("SubscriptionTopic/encounter-example");
        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r5/subscriptiontopic-encounter-example.json", "data/r5/encounter-virtual-planned.json")]
    public void SubscriptionCreateNotTriggered(string subscriptionJson, string encounterJson)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "SubscriptionTopic",
            subscriptionJson,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
            out string serializedResource,
            out string serializedOutcome,
            out string eTag,
            out string lastModified,
            out string location);

        scCreate.Should().Be(HttpStatusCode.Created);
        location.Should().StartWith("SubscriptionTopic/");

        scCreate = fhirStore.InstanceCreate(
            "Encounter",
            encounterJson,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        scCreate.Should().Be(HttpStatusCode.Created);
        location.Should().StartWith("Encounter/");

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-example.json",
        "data/r5/subscription-encounter-example.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-completed.json")]
    public void SubscriptionCreateTriggered(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            topicJson, 
            fhirStore, 
            out serializedResource, 
            out serializedOutcome, 
            out eTag, 
            out lastModified, 
            out location);

        DoCreate(
            "Subscription",
            subscriptionJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Patient",
            patientJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        DoCreate(
            "Encounter",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        //_testOutputHelper.WriteLine(bundle);
    }

    /// <summary>Executes the create operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="json">              The JSON.</param>
    /// <param name="fhirStore">         The FHIR store.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    private static void DoCreate(
        string resourceType,
        string json,
        IFhirStore fhirStore, 
        out string serializedResource, 
        out string serializedOutcome, 
        out string eTag, 
        out string lastModified, 
        out string location)
    {
        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            resourceType,
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            true,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);
        scCreate.Should().Be(HttpStatusCode.Created);
        location.Should().StartWith(resourceType);
    }
}