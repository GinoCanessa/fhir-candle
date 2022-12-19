// <copyright file="ResourceStoreBasicTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using FhirServerHarness.Storage;
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

    public static IEnumerable<object[]> Configurations => new List<object[]>
    {
        new object[] { new ProviderConfiguration()
        {
            FhirVersion = ProviderConfiguration.FhirVersionCodes.R4B,
            TenantRoute = "r4b",
        } },
    };

    /// <summary>Creates FHIR store.</summary>
    /// <param name="config">The configuration.</param>
    [Theory]
    [MemberData(nameof(Configurations))]
    public void CreateFhirStore(ProviderConfiguration config)
    {
        IFhirStore fhirStore = new FhirStore();
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