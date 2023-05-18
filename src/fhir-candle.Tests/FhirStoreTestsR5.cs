// <copyright file="FhirStoreTestsR5.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias storeR5;

using FhirStore.Models;
using FhirStore.Storage;
using fhir.candle.Tests.Extensions;
using fhir.candle.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;
using storeR5::FhirStore.Models;
using storeR5::FhirStore.Storage;
using Hl7.Fhir.Model;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore R5 functionality.</summary>
public class FhirStoreTestsR5: IDisposable
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly TenantConfiguration _config = new()
    {
        FhirVersion = TenantConfiguration.SupportedFhirVersions.R5,
        ControllerName = "r5",
        BaseUrl = "http://localhost/fhir/r5",
    };

    private const int _expectedRestResources = 157;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR5"/> class.
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

    [Theory]
    [FileData("data/r5/patient-example.json")]
    public void PatientCreateRead(string json)
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
        location.Should().EndWith("Patient/example");

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
        location.Should().Contain("Patient/");
    }

    [Theory]
    [FileData("data/r5/patient-invalid.json")]
    public void PatientCreateInvalid(string json)
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

        scCreate.Should().Be(HttpStatusCode.BadRequest);
        serializedResource.Should().BeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().BeNullOrEmpty();
        lastModified.Should().BeNullOrEmpty();
        location.Should().BeNullOrEmpty();
    }

    [Theory]
    [FileData("data/r5/patient-example.json")]
    public void PatientCreateWrongLocation(string json)
    {
        //_testOutputHelper.WriteLine(json);

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        HttpStatusCode scCreate = fhirStore.InstanceCreate(
            "Encounter",
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

        scCreate.Should().Be(HttpStatusCode.UnprocessableEntity);
        serializedResource.Should().BeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().BeNullOrEmpty();
        lastModified.Should().BeNullOrEmpty();
        location.Should().BeNullOrEmpty();
    }

    [Theory]
    [FileData("data/r5/Observation-example.json")]
    public void ObservationCreateRead(string json)
    {
        //_testOutputHelper.WriteLine(json);

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "Observation",
            json,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith("Observation/example");

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
        location.Should().Contain("Observation/");
    }


    [Theory]
    [FileData("data/r5/Observation-example.json")]
    public void ObservationUpdate(string json)
    {
        //_testOutputHelper.WriteLine(json);

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "Observation",
            json,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        serializedResource.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith("Observation/example");

        json = json.Replace("185,", "180,");

        DoUpdate(
            "Observation",
            "example",
            json,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        serializedResource.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"2\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith("Observation/example");
    }

    [Theory]
    [FileData("data/r5/searchparameter-patient-multiplebirth.json")]
    public void SearchParameterCreate(string json)
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
        location.Should().Contain("SearchParameter/");

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
    public void SearchParameterCreateCapabilityCount(string json)
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
    public void SubscriptionTopicCreateRead(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        DoCreate(
            "SubscriptionTopic",
            json,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith("SubscriptionTopic/encounter-example");

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
    [FileData(
        "data/r5/subscriptiontopic-encounter-example.json",
        "data/r5/subscription-encounter-example.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-planned.json")]
    public void SubscriptionNotTriggered(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
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

        string notification = fhirStore.SerializeSubscriptionEvents(
            "example",
            new long[1] { 1 },
            "notification-event");

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(1);

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-example.json",
        "data/r5/subscription-encounter-example.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-completed.json")]
    public void SubscriptionTriggeredCreate(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
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

        string notification = fhirStore.SerializeSubscriptionEvents(
            "example",
            Array.Empty<long>(),
            "notification-event");

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(2);

        //_testOutputHelper.WriteLine(bundle);
    }


    [Theory]
    [FileData(
        "data/r5/subscriptiontopic-encounter-example.json",
        "data/r5/subscription-encounter-example.json",
        "data/r5/patient-example.json",
        "data/r5/encounter-virtual-in-progress.json")]
    public void SubscriptionTriggeredUpdate(string topicJson, string subscriptionJson, string patientJson, string encounterJson)
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

        string notification = fhirStore.SerializeSubscriptionEvents(
            "example",
            Array.Empty<long>(),
            "notification-event");

        notification.Should().NotBeEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(1);

        encounterJson = encounterJson.Replace("in-progress", "completed");

        DoUpdate(
            "Encounter",
            "virtual-in-progress",
            encounterJson,
            fhirStore,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);

        notification = fhirStore.SerializeSubscriptionEvents(
            "example",
            Array.Empty<long>(),
            "notification-event");

        notification.Should().NotBeEmpty();

        results = JsonSerializer.Deserialize<MinimalBundle>(notification);

        results.Should().NotBeNull();
        results!.Entries.Should().HaveCount(2);

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
    private static HttpStatusCode DoCreate(
        string resourceType,
        string json,
        IFhirStore fhirStore, 
        out string serializedResource, 
        out string serializedOutcome, 
        out string eTag, 
        out string lastModified, 
        out string location)
    {
        HttpStatusCode sc = fhirStore.InstanceCreate(
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
        sc.Should().Be(HttpStatusCode.Created);
        location.Should().Contain(resourceType);
        return sc;
    }

    /// <summary>Executes the update operation.</summary>
    /// <param name="resourceType">      Type of the resource.</param>
    /// <param name="id">                Id of the resource we are updating.</param>
    /// <param name="json">              The JSON.</param>
    /// <param name="fhirStore">         The FHIR store.</param>
    /// <param name="serializedResource">[out] The serialized resource.</param>
    /// <param name="serializedOutcome"> [out] The serialized outcome.</param>
    /// <param name="eTag">              [out] The tag.</param>
    /// <param name="lastModified">      [out] The last modified.</param>
    /// <param name="location">          [out] The location.</param>
    private static HttpStatusCode DoUpdate(
        string resourceType,
        string id,
        string json,
        IFhirStore fhirStore,
        out string serializedResource,
        out string serializedOutcome,
        out string eTag,
        out string lastModified,
        out string location)
    {
        HttpStatusCode sc = fhirStore.InstanceUpdate(
            resourceType,
            id,
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            string.Empty,
            true,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified,
            out location);
        sc.Should().Be(HttpStatusCode.OK);
        location.Should().Contain(resourceType);
        return sc;
    }
}