// <copyright file="ResourceStoreBasicTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Tests.Extensions;
using FhirServerHarness.Ucum;
using FluentAssertions;
using System.Net;
using Xunit.Abstractions;

namespace FhirServerHarness.Tests;

/// <summary>Unit tests core UCUM functional.ity.</summary>
public class UcumTests : IDisposable
{
    private readonly ITestOutputHelper _testOutputHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="FhirStoreTestsR4B"/> class.
    /// </summary>
    /// <param name="testOutputHelper">The test output helper.</param>
    public UcumTests(ITestOutputHelper testOutputHelper)
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

    [Fact]
    public void BasicParseTestAgeY()
    {
        //_testOutputHelper.WriteLine(json);

        string val = "1 a";

        bool result = UcumUtils.isValidUcum(val);

        result.Should().BeTrue();
    }
}