// <copyright file="FhirStoreTestsR4BPatient.cs" company="Microsoft Corporation">
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
public class FhirStoreTestsR4BPatient : IDisposable
{
    /// <summary>The FHIR store.</summary>
    private static IFhirStore _store;

    /// <summary>(Immutable) The configuration.</summary>
    private static readonly ProviderConfiguration _config = new()
    {
        FhirVersion = ProviderConfiguration.FhirVersionCodes.R4B,
        TenantRoute = "r4b",
    };

    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>(Immutable) The total patients expected.</summary>
    private const int _expectedTotal = 5;

    /// <summary>(Immutable) The expected male.</summary>
    private const int _expectedMale = 3;

    /// <summary>(Immutable) The expected female.</summary>
    private const int _expectedFemale = 1;

    /// <summary>
    /// Initializes static members of the FhirServerHarness.Tests.FhirStoreTestsR4BPatient class.
    /// </summary>
    static FhirStoreTestsR4BPatient()
    {
        _store = new VersionedFhirStore();
        _store.Init(_config);

        string path = Path.GetRelativePath(Directory.GetCurrentDirectory(), "data/r4b");

        foreach (string filename in Directory.EnumerateFiles(path, "patient-*.json", SearchOption.TopDirectoryOnly))
        {
            _ = _store.InstanceCreate(
                "Patient",
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

        foreach (string filename in Directory.EnumerateFiles(path, "searchparameter-patient*.json", SearchOption.TopDirectoryOnly))
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
    public FhirStoreTestsR4BPatient(ITestOutputHelper testOutputHelper)
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
    [InlineData("_id:not=example", (_expectedTotal - 1))]
    [InlineData("name=peter", 1)]
    [InlineData("name=not-present,another-not-present", 0)]
    [InlineData("name=peter,not-present", 1)]
    [InlineData("name=not-present,peter", 1)]
    [InlineData("name:contains=eter", 1)]
    [InlineData("name:contains=zzrot", 0)]
    [InlineData("name:exact=Peter", 1)]
    [InlineData("name:exact=peter", 0)]
    [InlineData("name:exact=Peterish", 0)]
    [InlineData("_profile:missing=true", _expectedTotal)]
    [InlineData("_profile:missing=false", 0)]
    [InlineData("multiplebirth=3", 1)]
    [InlineData("multiplebirth=le3", 1)]
    [InlineData("multiplebirth=lt3", 0)]
    [InlineData("birthdate=1982-01-23", 1)]
    [InlineData("birthdate=1982-01", 1)]
    [InlineData("birthdate=1982", 2)]
    [InlineData("gender=male", _expectedMale)]
    [InlineData("gender=female", _expectedFemale)]
    [InlineData("gender=male,female", (_expectedMale + _expectedFemale))]
    [InlineData("name-use=official", _expectedTotal)]
    [InlineData("name-use=invalid-name-use", 0)]
    public void PatientSearchWithCount(string search, int matchCount)
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