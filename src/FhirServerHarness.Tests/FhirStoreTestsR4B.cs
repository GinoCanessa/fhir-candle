// <copyright file="ResourceStoreBasicTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using FhirServerHarness.Storage;
using FhirServerHarness.Tests.Extensions;
using FluentAssertions;
using Hl7.Fhir.Model;
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

    private readonly IFhirStore _fhirStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR4B(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;

        _fhirStore = new FhirStore();
        _fhirStore.Init(_config);
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
    public void StoreExamplePatient(string json)
    {
        //_testOutputHelper.WriteLine(json);

        HttpStatusCode scCreate = _fhirStore.InstanceCreate(
            "Patient",
            json,
            "application/fhir+json",
            "application/fhir+json",
            string.Empty,
            out string serializedResource,
            out string serializedOutcome,
            out string eTag,
            out string lastModified);

        scCreate.Should().Be(HttpStatusCode.Created);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();

        HttpStatusCode scRead = _fhirStore.InstanceRead(
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
        lastModified.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [DirectoryContentsData("data/r4b", "patient-*.json")]
    public void LoadAndSearch(params string[] jsons)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        foreach (var json in jsons)
        {
            HttpStatusCode scCreate = _fhirStore.InstanceCreate(
                "Patient",
                json,
                "application/fhir+json",
                "application/fhir+json",
                string.Empty,
                out string serializedResource,
                out string serializedOutcome,
                out string eTag,
                out string lastModified);

            scCreate.Should().Be(HttpStatusCode.Created);
            serializedResource.Should().NotBeNullOrEmpty();
            serializedOutcome.Should().NotBeNullOrEmpty();
            eTag.Should().Be("W/\"1\"");
            lastModified.Should().NotBeNullOrEmpty();
        }
    }
}