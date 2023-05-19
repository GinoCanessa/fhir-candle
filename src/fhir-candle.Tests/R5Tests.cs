// <copyright file="FhirStoreTestsR5Resource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias storeR5;

using FhirStore.Models;
using FhirStore.Storage;
using fhir.candle.Tests.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit.Abstractions;
using storeR5::FhirStore.Models;
using storeR5::FhirStore.Storage;

namespace fhir.candle.Tests;

/// <summary>Unit tests for FHIR R5.</summary>
public class R5Tests
{
    /// <summary>The FHIR store.</summary>
    internal IFhirStore _store;

    /// <summary>(Immutable) The configuration.</summary>
    internal readonly TenantConfiguration _config;

    /// <summary>(Immutable) The total number of patients.</summary>
    internal const int _patientCount = 5;

    /// <summary>(Immutable) The number of patients coded as male.</summary>
    internal const int _patientsMale = 3;

    /// <summary>(Immutable) The number of patients coded as female.</summary>
    internal const int _patientsFemale = 1;

    /// <summary>(Immutable) The total number of observations.</summary>
    internal const int _observationCount = 6;

    /// <summary>(Immutable) The number of observations that are vital signs.</summary>
    internal const int _observationsVitalSigns = 3;

    /// <summary>(Immutable) The number of observations with the subject 'example'.</summary>
    internal const int _observationsWithSubjectExample = 4;

    /// <summary>(Immutable) Number of encounters.</summary>
    internal const int _encounterCount = 1;

    /// <summary>(Immutable) Number of subscription topics.</summary>
    internal const int _subscriptionTopicCount = 1;

    /// <summary>Initializes a new instance of the <see cref="R5Tests"/> class.</summary>
    public R5Tests()
    {
        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r5");
        DirectoryInfo? loadDirectory = null;

        if (Directory.Exists(path))
        {
            loadDirectory = new DirectoryInfo(path);
        }

        _config = new()
        {
            FhirVersion = TenantConfiguration.SupportedFhirVersions.R5,
            ControllerName = "r5",
            BaseUrl = "http://localhost/fhir/r5",
            LoadDirectory = loadDirectory,
        };

        _store = new VersionedFhirStore();
        _store.Init(_config);
    }
}

/// <summary>Test R5 patient looped.</summary>
public class R5TestsPatientLooped : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="R5TestsPatientLooped"/> class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R5TestsPatientLooped(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id=example", 100)]
    public void LoopedPatientsSearch(string search, int loopCount)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        for (int i = 0; i < loopCount; i++)
        {
            _fixture._store.TypeSearch("Patient", search, "application/fhir+json", out string bundle, out string outcome);
            bundle.Should().NotBeNullOrEmpty();
        }
    }
}

/// <summary>Test R5 Observation searches.</summary>
public class R5TestsObservation : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R5TestsObservation"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R5TestsObservation(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id:not=example", (R5Tests._observationCount - 1))]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id=example", 1)]
    [InlineData("_id=example&_include=Observation:patient", 1, 2)]
    //[InlineData("code-value-quantity=http://loinc.org|29463-7$185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=185|http://unitsofmeasure.org|lbs", 1)]
    [InlineData("value-quantity=185||[lb_av]", 1)]
    [InlineData("value-quantity=185||lbs", 1)]
    [InlineData("value-quantity=185", 1)]
    [InlineData("value-quantity=ge185|http://unitsofmeasure.org|[lb_av]", 1)]
    [InlineData("value-quantity=ge185||[lb_av]", 1)]
    [InlineData("value-quantity=ge185||lbs", 1)]
    [InlineData("value-quantity=ge185", 2)]
    [InlineData("value-quantity=gt185|http://unitsofmeasure.org|[lb_av]", 0)]
    [InlineData("value-quantity=gt185||[lb_av]", 0)]
    [InlineData("value-quantity=gt185||lbs", 0)]
    [InlineData("value-quantity=84.1|http://unitsofmeasure.org|[kg]", 0)]       // TODO: test unit conversion
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|265201", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cL/s", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cl/s", 1)]
    [InlineData("value-quantity=820||265201", 1)]
    [InlineData("value-quantity=820||cL/s", 1)]
    [InlineData("subject=Patient/example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject=Patient/UnknownPatientId", 0)]
    [InlineData("subject=example", R5Tests._observationsWithSubjectExample)]
    [InlineData("code=http://loinc.org|9272-6", 1)]
    [InlineData("code=http://snomed.info/sct|169895004", 1)]
    [InlineData("code=http://snomed.info/sct|9272-6", 0)]
    [InlineData("_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", R5Tests._observationsVitalSigns)]
    [InlineData("_profile:missing=true", (R5Tests._observationCount - R5Tests._observationsVitalSigns))]
    [InlineData("_profile:missing=false", R5Tests._observationsVitalSigns)]
    [InlineData("subject.name=peter", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient.name=peter", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject._id=example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient._id=example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject._id=example&_include=Observation:patient", R5Tests._observationsWithSubjectExample, R5Tests._observationsWithSubjectExample + 1)]
    public void ObservationSearch(string search, int matchCount, int? entryCount = null)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        _fixture._store.TypeSearch("Observation", search, "application/fhir+json", out string bundle, out _);

        bundle.Should().NotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(bundle);

        results.Should().NotBeNull();
        results!.Total.Should().Be(matchCount);
        if (entryCount != null)
        {
            results!.Entries.Should().HaveCount((int)entryCount);
        }

        results!.Links.Should().NotBeNullOrEmpty();
        string selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.Should().NotBeNullOrEmpty();
        selfLink.Should().StartWith(_fixture._config.BaseUrl + "/Observation?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.Should().Contain(searchPart);
        }

        //_testOutputHelper.WriteLine(bundle);
    }
}

/// <summary>Test R5 Patient searches.</summary>
public class R5TestsPatient : IClassFixture<R5Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R5Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R5TestsPatient"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R5TestsPatient(R5Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id:not=example", (R5Tests._patientCount - 1))]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id=example", 1)]
    [InlineData("_id=example&_revinclude=Observation:patient", 1, (R5Tests._observationsWithSubjectExample + 1))]
    [InlineData("name=peter", 1)]
    [InlineData("name=not-present,another-not-present", 0)]
    [InlineData("name=peter,not-present", 1)]
    [InlineData("name=not-present,peter", 1)]
    [InlineData("name:contains=eter", 1)]
    [InlineData("name:contains=zzrot", 0)]
    [InlineData("name:exact=Peter", 1)]
    [InlineData("name:exact=peter", 0)]
    [InlineData("name:exact=Peterish", 0)]
    [InlineData("_profile:missing=true", R5Tests._patientCount)]
    [InlineData("_profile:missing=false", 0)]
    [InlineData("multiplebirth=3", 1)]
    [InlineData("multiplebirth=le3", 1)]
    [InlineData("multiplebirth=lt3", 0)]
    [InlineData("birthdate=1982-01-23", 1)]
    [InlineData("birthdate=1982-01", 1)]
    [InlineData("birthdate=1982", 2)]
    [InlineData("gender=InvalidValue", 0)]
    [InlineData("gender=male", R5Tests._patientsMale)]
    [InlineData("gender=female", R5Tests._patientsFemale)]
    [InlineData("gender=male,female", (R5Tests._patientsMale + R5Tests._patientsFemale))]
    [InlineData("name-use=official", R5Tests._patientCount)]
    [InlineData("name-use=invalid-name-use", 0)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData("identifier=|12345", 1)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData("active=true", R5Tests._patientCount)]
    [InlineData("active=false", 0)]
    [InlineData("active=garbage", 0)]
    [InlineData("telecom=phone|(03) 5555 6473", 1)]
    [InlineData("telecom=|(03) 5555 6473", 1)]
    [InlineData("telecom=phone|", 1)]
    [InlineData("_id=example&name=peter", 1)]
    [InlineData("_id=example&name=not-present", 0)]
    [InlineData("_id=example&_profile:missing=false", 0)]
    public void PatientSearch(string search, int matchCount, int? entryCount = null)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        _fixture._store.TypeSearch("Patient", search, "application/fhir+json", out string bundle, out _);

        bundle.Should().NotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(bundle);

        results.Should().NotBeNull();
        results!.Total.Should().Be(matchCount);
        if (entryCount != null)
        {
            results!.Entries.Should().HaveCount((int)entryCount);
        }

        results!.Links.Should().NotBeNullOrEmpty();
        string selfLink = results!.Links!.Where(l => l.Relation.Equals("self"))?.Select(l => l.Url).First() ?? string.Empty;
        selfLink.Should().NotBeNullOrEmpty();
        selfLink.Should().StartWith(_fixture._config.BaseUrl + "/Patient?");
        foreach (string searchPart in search.Split('&'))
        {
            selfLink.Should().Contain(searchPart);
        }

        //_testOutputHelper.WriteLine(bundle);
    }
}