// <copyright file="ResourceStoreBasicTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using FhirServerHarness.Storage;
using FhirServerHarness.Tests.Extensions;
using FluentAssertions;
using System.Net;
using Xunit.Abstractions;

namespace FhirServerHarness.Tests;

/// <summary>Unit tests core FhirStore functionality.</summary>
public class FhirStoreTestsR4B: IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    private readonly ProviderConfiguration _config = new ()
    {
        FhirVersion = ProviderConfiguration.FhirVersionCodes.R4B,
        TenantRoute = "r4b",
    };

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
    [FileData("data/r4b/patient-example.json")]
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
    [FileData("data/r4b/Observation-example.json")]
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
    [FileData("data/r4b/searchparameter-patient-multiplebirth.json")]
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
}