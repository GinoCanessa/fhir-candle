﻿// <copyright file="FhirStoreTestsR4Resource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

extern alias candleR4;

using FhirCandle.Models;
using FhirCandle.Storage;
using fhir.candle.Tests.Models;
using FluentAssertions;
using System.Text.Json;
using Xunit.Abstractions;
using candleR4::FhirCandle.Models;
using candleR4::FhirCandle.Storage;
using static FhirCandle.Storage.Common;
using fhir.candle.Tests.Extensions;
using Hl7.Fhir.Language.Debugging;
using System.Net;
using Hl7.Fhir.Model;
using System.Reflection.Metadata;

namespace fhir.candle.Tests;

/// <summary>Unit tests for FHIR R4.</summary>
public class R4Tests
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

    /// <summary>Initializes a new instance of the <see cref="R4Tests"/> class.</summary>
    public R4Tests()
    {
        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4");
        DirectoryInfo? loadDirectory = null;

        if (Directory.Exists(path))
        {
            loadDirectory = new DirectoryInfo(path);
        }

        _config = new()
        {
            FhirVersion = TenantConfiguration.SupportedFhirVersions.R4,
            ControllerName = "r4",
            BaseUrl = "http://localhost/fhir/r4",
            LoadDirectory = loadDirectory,
        };

        _store = new VersionedFhirStore();
        _store.Init(_config);
    }
}

/// <summary>Test R4 patient looped.</summary>
public class R4TestsPatientLooped : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the <see cref="R4TestsPatientLooped"/> class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestsPatientLooped(R4Tests fixture, ITestOutputHelper testOutputHelper)
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
            _fixture._store.TypeSearch("Patient", search, "application/fhir+json", string.Empty, false, out string bundle, out string outcome);
            bundle.Should().NotBeNullOrEmpty();
        }
    }
}

/// <summary>Test R4 Observation searches.</summary>
public class R4TestsObservation : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R4TestsObservation"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4TestsObservation(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id:not=example", (R4Tests._observationCount - 1))]
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
    [InlineData("subject=Patient/example", R4Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient=Patient/example", R5Tests._observationsWithSubjectExample)]
    [InlineData("subject:Device=Patient/example", 0)]
    [InlineData("subject=Patient/UnknownPatientId", 0)]
    [InlineData("subject=example", R4Tests._observationsWithSubjectExample)]
    [InlineData("code=http://loinc.org|9272-6", 1)]
    [InlineData("code=http://snomed.info/sct|169895004", 1)]
    [InlineData("code=http://snomed.info/sct|9272-6", 0)]
    [InlineData("_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", R4Tests._observationsVitalSigns)]
    [InlineData("_profile:missing=true", (R4Tests._observationCount - R4Tests._observationsVitalSigns))]
    [InlineData("_profile:missing=false", R4Tests._observationsVitalSigns)]
    [InlineData("subject.name=peter", R4Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient.name=peter", R4Tests._observationsWithSubjectExample)]
    [InlineData("subject._id=example", R4Tests._observationsWithSubjectExample)]
    [InlineData("subject:Patient._id=example", R4Tests._observationsWithSubjectExample)]
    [InlineData("subject._id=example&_include=Observation:patient", R4Tests._observationsWithSubjectExample, R4Tests._observationsWithSubjectExample + 1)]
    public void ObservationSearch(string search, int matchCount, int? entryCount = null)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        _fixture._store.TypeSearch("Observation", search, "application/fhir+json", string.Empty, false, out string bundle, out _);

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

/// <summary>Test R4 Patient searches.</summary>
public class R4TestsPatient : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>Initializes a new instance of the <see cref="R4TestsPatient"/> class.</summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">The test output helper.</param>
    public R4TestsPatient(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    [Theory]
    [InlineData("_id:not=example", (R4Tests._patientCount - 1))]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id=example", 1)]
    [InlineData("_id=example&_revinclude=Observation:patient", 1, (R4Tests._observationsWithSubjectExample + 1))]
    [InlineData("name=peter", 1)]
    [InlineData("name=not-present,another-not-present", 0)]
    [InlineData("name=peter,not-present", 1)]
    [InlineData("name=not-present,peter", 1)]
    [InlineData("name:contains=eter", 1)]
    [InlineData("name:contains=zzrot", 0)]
    [InlineData("name:exact=Peter", 1)]
    [InlineData("name:exact=peter", 0)]
    [InlineData("name:exact=Peterish", 0)]
    [InlineData("_profile:missing=true", R4Tests._patientCount)]
    [InlineData("_profile:missing=false", 0)]
    [InlineData("multiplebirth=3", 1)]
    [InlineData("multiplebirth=le3", 1)]
    [InlineData("multiplebirth=lt3", 0)]
    [InlineData("birthdate=1982-01-23", 1)]
    [InlineData("birthdate=1982-01", 1)]
    [InlineData("birthdate=1982", 2)]
    [InlineData("gender=InvalidValue", 0)]
    [InlineData("gender=male", R4Tests._patientsMale)]
    [InlineData("gender=female", R4Tests._patientsFemale)]
    [InlineData("gender=male,female", (R4Tests._patientsMale + R4Tests._patientsFemale))]
    [InlineData("name-use=official", R4Tests._patientCount)]
    [InlineData("name-use=invalid-name-use", 0)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData("identifier=|12345", 1)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData("active=true", R4Tests._patientCount)]
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

        _fixture._store.TypeSearch("Patient", search, "application/fhir+json", string.Empty, false, out string bundle, out _);

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

/// <summary>A test subscription internals.</summary>
public class R4TestSubscriptions : IClassFixture<R4Tests>
{
    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>Gets the configurations.</summary>
    public static IEnumerable<object[]> Configurations => FhirStoreTests.TestConfigurations;

    /// <summary>(Immutable) The fixture.</summary>
    private readonly R4Tests _fixture;

    /// <summary>
    /// Initializes a new instance of the fhir.candle.Tests.TestSubscriptionInternals class.
    /// </summary>
    /// <param name="fixture">         (Immutable) The fixture.</param>
    /// <param name="testOutputHelper">(Immutable) The test output helper.</param>
    public R4TestSubscriptions(R4Tests fixture, ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Parse topic.</summary>
    /// <param name="json">The JSON.</param>
    [Theory]
    [FileData("data/r4/Basic-topic-encounter-complete-qualified.json")]
    [FileData("data/r4/Basic-topic-encounter-complete.json")]
    public void ParseTopic(string json)
    {
        HttpStatusCode sc = candleR4.FhirCandle.Serialization.Utils.TryDeserializeFhir(
            json, 
            "application/fhir+json", 
            out Hl7.Fhir.Model.Resource? r, 
            out _);

        sc.Should().Be(HttpStatusCode.OK);
        r.Should().NotBeNull();
        r!.TypeName.Should().Be("Basic");

        candleR4.FhirCandle.Subscriptions.TopicConverter converter = new candleR4.FhirCandle.Subscriptions.TopicConverter();

        bool success = converter.TryParse(r, out ParsedSubscriptionTopic s);

        success.Should().BeTrue();
        s.Should().NotBeNull();
        s.Id.Should().Be("encounter-complete");
        s.Url.Should().Be("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.ResourceTriggers.Should().HaveCount(1);
        s.ResourceTriggers.Keys.Should().Contain("Encounter");
        s.EventTriggers.Should().BeEmpty();
        s.AllowedFilters.Should().NotBeEmpty();
        s.AllowedFilters.Keys.Should().Contain("Encounter");
        s.NotificationShapes.Should().NotBeEmpty();
        s.NotificationShapes.Keys.Should().Contain("Encounter");
    }

    [Theory]
    [FileData("data/r4/Subscription-encounter-complete.json")]
    public void ParseSubscription(string json)
    {
        HttpStatusCode sc = candleR4.FhirCandle.Serialization.Utils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.Should().Be(HttpStatusCode.OK);
        r.Should().NotBeNull();
        r!.TypeName.Should().Be("Subscription");

        candleR4.FhirCandle.Subscriptions.SubscriptionConverter converter = new candleR4.FhirCandle.Subscriptions.SubscriptionConverter();

        bool success = converter.TryParse(r, out ParsedSubscription s);

        success.Should().BeTrue();
        s.Should().NotBeNull();
        s.Id.Should().Be("383c610b-8a8b-4173-b363-7b811509aadd");
        s.TopicUrl.Should().Be("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.Filters.Should().HaveCount(1);
        s.ChannelCode.Should().Be("rest-hook");
        s.Endpoint.Should().Be("https://subscriptions.argo.run/fhir/r4/$subscription-hook");
        s.HeartbeatSeconds.Should().Be(120);
        s.TimeoutSeconds.Should().BeNull();
        s.ContentType.Should().Be("application/fhir+json");
        s.ContentLevel.Should().Be("id-only");
        s.CurrentStatus.Should().Be("active");
    }

    [Theory]
    [FileData("data/r4/Bundle-notification-handshake.json")]
    public void ParseHandshake(string json)
    {
        HttpStatusCode sc = candleR4.FhirCandle.Serialization.Utils.TryDeserializeFhir(
            json,
            "application/fhir+json",
            out Hl7.Fhir.Model.Resource? r,
            out _);

        sc.Should().Be(HttpStatusCode.OK);
        r.Should().NotBeNull();
        r!.TypeName.Should().Be("Bundle");

        ParsedSubscriptionStatus? s = ((VersionedFhirStore)_fixture._store).ParseNotificationBundle((Hl7.Fhir.Model.Bundle)r);

        s.Should().NotBeNull();
        s!.BundleId.Should().Be("64578ab3-2bf6-497a-a873-7c29fa2090d6");
        s.SubscriptionReference.Should().Be("https://subscriptions.argo.run/fhir/r4/Subscription/383c610b-8a8b-4173-b363-7b811509aadd");
        s.SubscriptionTopicCanonical.Should().Be("http://example.org/FHIR/SubscriptionTopic/encounter-complete");
        s.Status.Should().Be("active");
        s.NotificationType.Should().Be(ParsedSubscription.NotificationTypeCodes.Handshake);
        s.NotificationEvents.Should().BeEmpty();
        s.Errors.Should().BeEmpty();
    }
}