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
public class FhirStoreTestsR4B : IDisposable
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
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void PatientsCreate(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        foreach (var json in jsons)
        {
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
            location.Should().StartWith("Patient/");
        }
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByIdExample(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "_id=example", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void PatientsCreateSearchByIdNotFound(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        foreach (var json in jsons)
        {
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
            location.Should().StartWith("Patient/");
        }

        fhirStore.TypeSearch("Patient", "_id=invalidIdToSearchFor", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThanOrEqualTo(55);       // empty bundle length

        //_testOutputHelper.WriteLine(bundle);
    }
    
    [Theory]
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void PatientsCreateSearchByIdExample(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        foreach (var json in jsons)
        {
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
            location.Should().StartWith("Patient/");
        }

        fhirStore.TypeSearch("Patient", "_id=example", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(55);       // empty bundle length

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void PatientsCreateSearchByNamePeter(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        foreach (var json in jsons)
        {
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
            location.Should().StartWith("Patient/");
        }

        fhirStore.TypeSearch("Patient", "name=peter", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(55);       // empty bundle length

        //_testOutputHelper.WriteLine(bundle);
    }


    [Theory]
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void PatientsCreateSearchByNameMultiple(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        foreach (var json in jsons)
        {
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
            location.Should().StartWith("Patient/");
        }

        fhirStore.TypeSearch("Patient", "name=invalid,peter", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(55);       // empty bundle length

        //_testOutputHelper.WriteLine(bundle);
    }


    [Theory]
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void LoopedPatientsCreateSearchByIdExample(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        IFhirStore fhirStore = new VersionedFhirStore();
        fhirStore.Init(_config);

        foreach (var json in jsons)
        {
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
            location.Should().StartWith("Patient/");
        }

        for (int i = 0; i < 100; i++)
        {
            fhirStore.TypeSearch("Patient", "_id=example", "application/fhir+json", out string bundle, out string outcome);
            bundle.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByNamePeterContains(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "name:contains=eter", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByNamePeterExact(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "name:exact=Peter", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByNamePeterExactNotFound(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "name:exact=peter", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThan(55);      // empty bundle

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByProfileMissingTrue(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "_profile:missing=true", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByProfileMissingFalse(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "_profile:missing=false", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThan(55);      // empty bundle

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example.json")]
    public void PatientCreateSearchByIdExampleNot(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "_id:not=example", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThan(55);      // empty set

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [TwoFileData("data/r4b/patient-example-d.json", "data/r4b/searchparameter-patient-multiplebirth.json")]
    public void PatientCreateSearchByMultipleBirth(string json, string json2)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json2,
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
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().StartWith("SearchParameter/");

        fhirStore.TypeSearch("Patient", "multiplebirth=3", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [TwoFileData("data/r4b/patient-example-d.json", "data/r4b/searchparameter-patient-multiplebirth.json")]
    public void PatientCreateSearchByMultipleBirthLeTrue(string json, string json2)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json2,
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
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().StartWith("SearchParameter/");

        fhirStore.TypeSearch("Patient", "multiplebirth=le3", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [TwoFileData("data/r4b/patient-example-d.json", "data/r4b/searchparameter-patient-multiplebirth.json")]
    public void PatientCreateSearchByMultipleBirthLtFalse(string json, string json2)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json2,
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
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().StartWith("SearchParameter/");

        fhirStore.TypeSearch("Patient", "multiplebirth=lt3", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThan(55);      // empty set

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example-c.json")]
    public void PatientCreateSearchByBirthDate(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "birthdate=1982-01-23", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example-a.json")]
    public void PatientCreateSearchByGenderMale(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "gender=male", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [FileData("data/r4b/patient-example-a.json")]
    public void PatientCreateSearchByGenderFemale(string json)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        fhirStore.TypeSearch("Patient", "gender=female", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThan(55);      // empty set

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [TwoFileData("data/r4b/patient-example.json", "data/r4b/searchparameter-patient-name-use.json")]
    public void PatientCreateSearchByNameUse(string json, string json2)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json2,
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
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().StartWith("SearchParameter/");

        fhirStore.TypeSearch("Patient", "name-use=official", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeGreaterThan(json.Length / 2);      // account for formatting change (pretty -> not)

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [TwoFileData("data/r4b/patient-example.json", "data/r4b/searchparameter-patient-name-use.json")]
    public void PatientCreateSearchByNameUseWrong(string json, string json2)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

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
        location.Should().StartWith("Patient/");

        scCreate = fhirStore.InstanceCreate(
            "SearchParameter",
            json2,
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
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().StartWith("SearchParameter/");

        fhirStore.TypeSearch("Patient", "name-use=wrong", "application/fhir+json", out string bundle, out string outcome);
        bundle.Should().NotBeNullOrEmpty();
        bundle.Length.Should().BeLessThan(55);      // empty set

        //_testOutputHelper.WriteLine(bundle);
    }
}