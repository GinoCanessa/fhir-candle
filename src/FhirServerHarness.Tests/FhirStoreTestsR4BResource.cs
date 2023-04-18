// <copyright file="FhirStoreTestsR4BResource.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Models;
using FhirServerHarness.Storage;
using FhirServerHarness.Tests.Extensions;
using FhirServerHarness.Tests.Models;
using FluentAssertions;
using System.Net;
using System.Text.Json;
using Xunit.Abstractions;

namespace FhirServerHarness.Tests;

/// <summary>Unit tests FhirStore Patient / search functionality.</summary>
public class FhirStoreTestsR4BResource : IDisposable
{
    /// <summary>The FHIR store.</summary>
    private static IFhirStore _store;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly ProviderConfiguration _config = new()
    {
        FhirVersion = Hl7.Fhir.Model.FHIRVersion.N4_1,
        TenantRoute = "r4b",
        BaseUrl = "http://localhost:5101/r4b",
    };

    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The total patients expected.</summary>
    private const int _patientCount = 5;

    /// <summary>(Immutable) The expected male.</summary>
    private const int _patientsMale = 3;

    /// <summary>(Immutable) The expected female.</summary>
    private const int _patientsFemale = 1;

    /// <summary>(Immutable) The total observations expected.</summary>
    private const int _observationCount = 6;

    /// <summary>(Immutable) The expected vital signs.</summary>
    private const int _observationsVitalSigns = 3;

    /// <summary>(Immutable) The expected subject example.</summary>
    private const int _observationsWithSubjects = 4;


    /// <summary>
    /// Initializes static members of the FhirServerHarness.Tests.FhirStoreTestsR4BPatient class.
    /// </summary>
    static FhirStoreTestsR4BResource()
    {
        _store = new VersionedFhirStore();
        _store.Init(_config);

        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4b");
        LoadTestJsons(path, "Patient");
        LoadTestJsons(path, "Observation");
    }

    /// <summary>Loads for resource.</summary>
    /// <param name="path">    Full pathname of the file.</param>
    /// <param name="resource">The resource.</param>
    private static void LoadTestJsons(string path, string resource)
    {
        string lower = resource.ToLowerInvariant();

        foreach (string filename in Directory.EnumerateFiles(path, $"{lower}-*.json", SearchOption.TopDirectoryOnly))
        {
            _ = _store.InstanceCreate(
                resource,
                File.ReadAllText(filename),
                "application/fhir+json",
                "application/fhir+json",
                string.Empty,
                true,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        foreach (string filename in Directory.EnumerateFiles(path, $"searchparameter-{lower}*.json", SearchOption.TopDirectoryOnly))
        {
            _ = _store.InstanceCreate(
                "SearchParameter",
                File.ReadAllText(filename),
                "application/fhir+json",
                "application/fhir+json",
                string.Empty,
                true,
                out _,
                out _,
                out _,
                out _,
                out _);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public FhirStoreTestsR4BResource(ITestOutputHelper testOutputHelper)
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
    [InlineData("_id=example", 100)]
    public void LoopedPatientsSearch(string search, int loopCount)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        for (int i = 0; i < loopCount; i++)
        {
            _store.TypeSearch("Patient", search, "application/fhir+json", out string bundle, out string outcome);
            bundle.Should().NotBeNullOrEmpty();
        }
    }

    [Theory]
    [InlineData("_id=example", 1)]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id:not=example", (_observationCount - 1))]
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
    [InlineData("value-quantity=84.1|http://unitsofmeasure.org|[kg]", 0)]       // test unit conversion
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|265201", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cL/s", 1)]
    [InlineData("value-quantity=820|urn:iso:std:iso:11073:10101|cl/s", 1)]
    [InlineData("value-quantity=820||265201", 1)]
    [InlineData("value-quantity=820||cL/s", 1)]
    [InlineData("subject=Patient/example", _observationsWithSubjects)]
    [InlineData("subject=Patient/UnknownPatientId", 0)]
    [InlineData("subject=example", _observationsWithSubjects)]
    [InlineData("code=http://loinc.org|9272-6", 1)]
    [InlineData("code=http://snomed.info/sct|169895004", 1)]
    [InlineData("code=http://snomed.info/sct|9272-6", 0)]
    [InlineData("_profile=http://hl7.org/fhir/StructureDefinition/vitalsigns", _observationsVitalSigns)]
    [InlineData("_profile:missing=true", (_observationCount - _observationsVitalSigns))]
    [InlineData("_profile:missing=false", _observationsVitalSigns)]
    public void ObservationSearch(string search, int matchCount)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        _store.TypeSearch("Observation", search, "application/fhir+json", out string bundle, out _);

        bundle.Should().NotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(bundle);

        results.Should().NotBeNull();
        results!.Total.Should().Be(matchCount);

        //_testOutputHelper.WriteLine(bundle);
    }

    [Theory]
    [InlineData("_id=example", 1)]
    [InlineData("_id=AnIdThatDoesNotExist", 0)]
    [InlineData("_id:not=example", (_patientCount - 1))]
    [InlineData("name=peter", 1)]
    [InlineData("name=not-present,another-not-present", 0)]
    [InlineData("name=peter,not-present", 1)]
    [InlineData("name=not-present,peter", 1)]
    [InlineData("name:contains=eter", 1)]
    [InlineData("name:contains=zzrot", 0)]
    [InlineData("name:exact=Peter", 1)]
    [InlineData("name:exact=peter", 0)]
    [InlineData("name:exact=Peterish", 0)]
    [InlineData("_profile:missing=true", _patientCount)]
    [InlineData("_profile:missing=false", 0)]
    [InlineData("multiplebirth=3", 1)]
    [InlineData("multiplebirth=le3", 1)]
    [InlineData("multiplebirth=lt3", 0)]
    [InlineData("birthdate=1982-01-23", 1)]
    [InlineData("birthdate=1982-01", 1)]
    [InlineData("birthdate=1982", 2)]
    [InlineData("gender=InvalidValue", 0)]
    [InlineData("gender=male", _patientsMale)]
    [InlineData("gender=female", _patientsFemale)]
    [InlineData("gender=male,female", (_patientsMale + _patientsFemale))]
    [InlineData("name-use=official", _patientCount)]
    [InlineData("name-use=invalid-name-use", 0)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|12345", 1)]
    [InlineData("identifier=|12345", 1)]
    [InlineData("identifier=urn:oid:1.2.36.146.595.217.0.1|ValueThatDoesNotExist", 0)]
    [InlineData("active=true", _patientCount)]
    [InlineData("active=false", 0)]
    [InlineData("active=garbage", 0)]
    [InlineData("telecom=phone|(03) 5555 6473", 1)]
    [InlineData("telecom=|(03) 5555 6473", 1)]
    [InlineData("telecom=phone|", 1)]
    [InlineData("_id=example&name=peter", 1)]
    [InlineData("_id=example&name=not-present", 0)]
    [InlineData("_id=example&_profile:missing=false", 0)]
    public void PatientSearch(string search, int matchCount)
    {
        //_testOutputHelper.WriteLine($"Running with {jsons.Length} files");

        _store.TypeSearch("Patient", search, "application/fhir+json", out string bundle, out _);

        bundle.Should().NotBeNullOrEmpty();

        MinimalBundle? results = JsonSerializer.Deserialize<MinimalBundle>(bundle);

        results.Should().NotBeNull();
        results!.Total.Should().Be(matchCount);

        //_testOutputHelper.WriteLine(bundle);
    }
}