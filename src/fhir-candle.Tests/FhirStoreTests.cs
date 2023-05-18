// <copyright file="FhirStoreTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias storeR4;
extern alias storeR4B;
extern alias storeR5;

using fhir.candle.Tests.Models;
using FhirStore.Models;
using FhirStore.Storage;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore functionality.</summary>
public class FhirStoreTests
{
    /// <summary>(Immutable) The configuration for FHIR R4.</summary>
    internal readonly TenantConfiguration _configR4;

    /// <summary>The FHIR store for FHIR R4.</summary>
    internal IFhirStore _storeR4;

    /// <summary>(Immutable) The configuration for FHIR R4B.</summary>
    internal readonly TenantConfiguration _configR4B;

    /// <summary>The FHIR store for FHIR R4B.</summary>
    internal IFhirStore _storeR4B;

    /// <summary>(Immutable) The configuration for FHIR R5.</summary>
    internal readonly TenantConfiguration _configR5;

    /// <summary>The FHIR store for FHIR R5.</summary>
    internal IFhirStore _storeR5;

    internal Dictionary<TenantConfiguration.SupportedFhirVersions, int> _expectedRestResources = new()
    {
        { TenantConfiguration.SupportedFhirVersions.R4, 146 },
        { TenantConfiguration.SupportedFhirVersions.R4B, 140 },
        { TenantConfiguration.SupportedFhirVersions.R5, 157 },
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTests"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTests()
    {
        _configR4 = new()
        {
            FhirVersion = TenantConfiguration.SupportedFhirVersions.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
        };

        _configR4B = new()
        {
            FhirVersion = TenantConfiguration.SupportedFhirVersions.R4B,
            ControllerName = "r4b",
            BaseUrl = "http://localhost/fhir/r4b",
        };

        _configR5 = new()
        {
            FhirVersion = TenantConfiguration.SupportedFhirVersions.R5,
            ControllerName = "r5",
            BaseUrl = "http://localhost/fhir/r5",
        };

        _storeR4 = new storeR4::FhirStore.Storage.VersionedFhirStore();
        _storeR4.Init(_configR4);

        _storeR4B = new storeR4B::FhirStore.Storage.VersionedFhirStore();
        _storeR4B.Init(_configR4B);

        _storeR5 = new storeR5::FhirStore.Storage.VersionedFhirStore();
        _storeR5.Init(_configR5);
    }

    /// <summary>Gets store for version.</summary>
    /// <exception cref="ArgumentException">Thrown when one or more arguments have unsupported or
    ///  illegal values.</exception>
    /// <param name="version">The version.</param>
    /// <returns>The store for version.</returns>
    public IFhirStore GetStoreForVersion(TenantConfiguration.SupportedFhirVersions version)
    {
        switch (version)
        {
            case TenantConfiguration.SupportedFhirVersions.R4:
                return _storeR4;

            case TenantConfiguration.SupportedFhirVersions.R4B:
                return _storeR4B;

            case TenantConfiguration.SupportedFhirVersions.R5:
                return _storeR5;
        }

        throw new ArgumentException($"Invalid version: {version}", nameof(version));
    }
}

/// <summary>A metadata test.</summary>
public class MetadataTest : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => new List<object[]>
    {
        new object[]
        {
            TenantConfiguration.SupportedFhirVersions.R4,
        },
        new object[]
        {
            TenantConfiguration.SupportedFhirVersions.R4B,
        },
        new object[]
        {
            TenantConfiguration.SupportedFhirVersions.R5,
        },
    };

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public MetadataTest(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void GetMetadata(TenantConfiguration.SupportedFhirVersions version)
    {
        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

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
        resourceCount.Should().Be(_fixture._expectedRestResources[version]);
    }
}