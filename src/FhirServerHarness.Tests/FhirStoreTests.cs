// <copyright file="FhirStoreTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias storeR4;
extern alias storeR4B;
extern alias storeR5;

using FhirStore.Models;
using FhirStore.Storage;
using FluentAssertions;
using Xunit.Abstractions;

namespace FhirServerHarness.Tests;

/// <summary>Unit tests core FhirStore functionality.</summary>
public class FhirStoreTests : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTests"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTests(ITestOutputHelper testOutputHelper)
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

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => new List<object[]>
    {
        new object[]
        {
            new ProviderConfiguration()
            {
                FhirVersion  = ProviderConfiguration.SupportedFhirVersions.R4,
                ControllerName = "r4",
                BaseUrl = "http://localhost/fhir/r5",
            },
        },
        new object[]
        {
            new ProviderConfiguration()
            {
                FhirVersion  = ProviderConfiguration.SupportedFhirVersions.R4B,
                ControllerName = "r4b",
                BaseUrl = "http://localhost/fhir/r5",
            },
        },
        new object[]
        {
            new ProviderConfiguration()
            {
                FhirVersion  = ProviderConfiguration.SupportedFhirVersions.R5,
                ControllerName = "r5",
                BaseUrl = "http://localhost/fhir/r5",
            },
        },
    };

    /// <summary>Creates FHIR store.</summary>
    /// <param name="config">The configuration.</param>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void CreateFhirStore(ProviderConfiguration config)
    {
        IFhirStore fhirStore;

        switch (config.FhirVersion)
        {
            case ProviderConfiguration.SupportedFhirVersions.R4:
                fhirStore = new storeR4::FhirStore.Storage.VersionedFhirStore();
                break;
            case ProviderConfiguration.SupportedFhirVersions.R4B:
                fhirStore = new storeR4B::FhirStore.Storage.VersionedFhirStore();
                break;
            case ProviderConfiguration.SupportedFhirVersions.R5:
                fhirStore = new storeR5::FhirStore.Storage.VersionedFhirStore();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(config), $"Unsupported FHIR Version: {config.FhirVersion}");
        }

        fhirStore.Should().NotBeNull("Failed to create FhirStore");

        // initialize with the provided configuration
        fhirStore.Init(config);

        // ensure we have at least 1 supported resource
        fhirStore.SupportedResources.Should().NotBeNullOrEmpty("FhirStore cannot support no resource types");

        // spot check a few resources
        fhirStore.SupportedResources.Should().Contain("Patient", "FhirStore should support Patient");
        fhirStore.SupportedResources.Should().Contain("Encounter", "FhirStore should support Encounter");
        fhirStore.SupportedResources.Should().Contain("Observation", "FhirStore should support Observation");
    }
}