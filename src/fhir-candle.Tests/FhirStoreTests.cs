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
using Hl7.Fhir.Model;
using System.Net;
using System.Security.AccessControl;
using System.Text.Json;
using System.Xml.Linq;
using Xunit.Abstractions;

namespace fhir.candle.Tests;

/// <summary>Unit tests core FhirStore functionality.</summary>
public class FhirStoreTests
{
    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> TestConfigurations => new List<object[]>
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
public class MetadataJson : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public MetadataJson(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
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
            out _,
            out _);

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

/// <summary>A metadata test.</summary>
public class MetadataXml : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public MetadataXml(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
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
            "application/fhir+xml",
            out string serializedResource,
            out string serializedOutcome,
            out _,
            out _);

        scRead.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();

        using (MemoryStream ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(serializedResource)))
        {
            XElement parsed = XElement.Load(ms);

            parsed.Should().NotBeNull();

            int resourceCount = parsed.Descendants("{http://hl7.org/fhir}resource").Count();
            resourceCount.Should().Be(_fixture._expectedRestResources[version]);
        }
    }
}

/// <summary>Create, read, update, and delete a Patient.</summary>
public class TestPatientCRUD : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType = "Patient";
    private const string _id = "common";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestPatientCRUD(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void PatientCRUD(TenantConfiguration.SupportedFhirVersions version)
    {
        string json1 = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + _id + "\",\"language\":\"en\"}";
        string json2 = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + _id + "\",\"language\":\"en-US\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        HttpStatusCode sc = fhirStore.InstanceCreate(
            _resourceType,
            json1,
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
        location.Should().Contain(_resourceType);

        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith(_resourceType + "/" + _id);

        sc = fhirStore.InstanceRead(
            _resourceType,
            _id,
            "application/fhir+json",
            string.Empty,
            eTag,
            lastModified,
            string.Empty,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        sc.Should().Be(HttpStatusCode.OK);
        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"1\"");
        location.Should().EndWith(_resourceType + "/" + _id);

        sc = fhirStore.InstanceUpdate(
            _resourceType,
            _id,
            json2,
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
        location.Should().Contain(_resourceType);

        serializedResource.Should().NotBeNullOrEmpty();
        serializedOutcome.Should().NotBeNullOrEmpty();
        eTag.Should().Be("W/\"2\"");
        lastModified.Should().NotBeNullOrEmpty();
        location.Should().EndWith(_resourceType + "/" + _id);

        sc = fhirStore.InstanceDelete(
            _resourceType,
            _id,
            "application/fhir+json",
            string.Empty,
            out serializedResource,
            out serializedOutcome);

        sc.Should().Be(HttpStatusCode.OK);
        location.Should().Contain(_resourceType);

        sc = fhirStore.InstanceRead(
            _resourceType,
            _id,
            "application/fhir+json",
            string.Empty,
            eTag,
            lastModified,
            string.Empty,
            out serializedResource,
            out serializedOutcome,
            out eTag,
            out lastModified);

        sc.Should().Be(HttpStatusCode.NotFound);
    }
}

/// <summary>Ensure that storing a Patient in the Observation endpoint fails.</summary>
public class TestResourceWrongLocation: IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType1 = "Patient";
    private const string _resourceType2 = "Observation";
    private const string _id = "common";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestResourceWrongLocation(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ResourceWrongLocation(TenantConfiguration.SupportedFhirVersions version)
    {
        string json = "{\"resourceType\":\"" + _resourceType1 + "\",\"id\":\"" + _id + "\",\"language\":\"en\"}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        HttpStatusCode sc = fhirStore.InstanceCreate(
            _resourceType2,
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

        sc.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}

/// <summary>Ensure that storing resources with invalid data fails.</summary>
public class TestResourceInvalidElement : IClassFixture<FhirStoreTests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    private const string _resourceType = "Patient";
    private const string _id = "invalid";

    /// <summary>(Immutable) The fixture.</summary>
    private readonly FhirStoreTests _fixture;

    public TestResourceInvalidElement(FhirStoreTests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [MemberData(nameof(Configurations))]
    public void ResourceWrongLocation(TenantConfiguration.SupportedFhirVersions version)
    {
        string json = "{\"resourceType\":\"" + _resourceType + "\",\"id\":\"" + _id + "\",\"garbage\":true}";

        IFhirStore fhirStore = _fixture.GetStoreForVersion(version);

        string serializedResource, serializedOutcome, eTag, lastModified, location;

        HttpStatusCode sc = fhirStore.InstanceCreate(
            _resourceType,
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

        sc.Should().Be(HttpStatusCode.BadRequest);
    }
}