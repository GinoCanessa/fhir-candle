// <copyright file="AuthTests.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using fhir.candle.Models;
using fhir.candle.Pages.Subscriptions;
using fhir.candle.Services;
using FhirCandle.Models;
using FhirCandle.Storage;
using FluentAssertions;
using Org.BouncyCastle.Asn1.Ocsp;
using System.Xml.Linq;
using Xunit.Abstractions;
using static System.Formats.Asn1.AsnWriter;

namespace fhir.candle.Tests;


public class AuthTests : IClassFixture<AuthTestFixture>
{
    /// <summary>(Immutable) The fixture.</summary>
    private readonly AuthTestFixture _fixture;

    /// <summary>(Immutable) The test output helper.</summary>
    private readonly ITestOutputHelper _testOutputHelper;

    public AuthTests(
        AuthTestFixture fixture,
        ITestOutputHelper testOutputHelper)
    {
        _fixture = fixture;
        _testOutputHelper = testOutputHelper;
    }

    /// <summary>Tests smart configuration.</summary>
    [Fact]
    public void TestSmartConfig()
    {
        _fixture.Should().NotBeNull();
        _fixture.AuthR4.Should().NotBeNull();
        _fixture.AuthR4.SmartConfigurationByTenant.Should().NotBeNullOrEmpty();

        FhirStore.Smart.SmartWellKnown smartWellKnown = _fixture.AuthR4.SmartConfigurationByTenant[_fixture.ConfigR4.ControllerName];

        smartWellKnown.Should().NotBeNull();
        smartWellKnown.GrantTypes.Should().NotBeNullOrEmpty();
        smartWellKnown.GrantTypes.Should().Contain("authorization_code");
        smartWellKnown.AuthorizationEndpoint.Should().NotBeNullOrEmpty();
        smartWellKnown.TokenEndpoint.Should().NotBeNullOrEmpty();
        smartWellKnown.TokenEndpointAuthMethods.Should().NotBeNullOrEmpty();
        smartWellKnown.SupportedScopes.Should().NotBeNullOrEmpty();
        smartWellKnown.SupportedScopes.Should().Contain("launch");
        smartWellKnown.SupportedScopes.Should().Contain("launch/patient");
        smartWellKnown.SupportedResponseTypes.Should().NotBeNullOrEmpty();
        smartWellKnown.SupportedResponseTypes.Should().Contain("code");
        smartWellKnown.SupportedResponseTypes.Should().Contain("id_token");
        smartWellKnown.Capabilities.Should().NotBeNullOrEmpty();
        smartWellKnown.Capabilities.Should().Contain("launch-standalone");
        smartWellKnown.Capabilities.Should().Contain("client-public");
        smartWellKnown.Capabilities.Should().Contain("permission-v1");
        smartWellKnown.Capabilities.Should().Contain("permission-v2");
        smartWellKnown.SupportedChallengeMethods.Should().NotBeNullOrEmpty();
        smartWellKnown.SupportedChallengeMethods.Should().Contain("S256");
    }

    [Theory]
    [InlineData(true, "http://localhost:5826/fhir/r4", "openid fhirUser profile launch/patient patient/*.read")]
    [InlineData(false, "http://localhost:5826/fhir/notAnEndpoint", "openid fhirUser profile launch/patient patient/*.read")]
    public void TestSmartAuthorize(
        bool expectSuccess,
        string audience,
        string scope)
    {
        string clientId = "clientid";
        string redirectUri = "http://localhost/dev/null";
        string? launch = null;
        string state = string.Empty;

        string pkceChallenge = string.Empty;
        string pkceMethod = string.Empty;

        bool success = _fixture.AuthR4.RequestAuth(
                _fixture.Name,
                string.Empty,
                "code",
                clientId,
                redirectUri,
                launch,
                scope,
                state,
                audience,
                pkceChallenge,
                pkceMethod,
                out string redirectDestination);

        success.Should().Be(expectSuccess);
        
        // stop testing if we failed, regardless of expectation
        if (!success)
        {
            return;
        }

        redirectDestination.Should().NotBeNullOrEmpty();

        string key = redirectDestination.Substring(redirectDestination.IndexOf("key=") + 4);
        key.Should().NotBeNullOrEmpty();

        _fixture.AuthR4.TryGetAuthorization(_fixture.Name, key, out AuthorizationInfo authInfo).Should().BeTrue();
        authInfo.Should().NotBeNull();
        authInfo.Tenant.Should().Be(_fixture.Name);
        authInfo.Expires.Should().BeAfter(DateTimeOffset.UtcNow);
        authInfo.RequestParameters.Should().NotBeNull();
        authInfo.Scopes.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("Patient/1", "")]
    public void TestTokenRequest(
        string launchPatient,
        string launchPractitioner)
    {
        _fixture.AuthR4.RequestAuth(
            _fixture.Name,
            string.Empty,
            "code",
            "clientId",
            "http://localhost/dev/null",
            string.Empty,
            "openid fhirUser profile launch/patient patient/*.read",
            string.Empty,
            _fixture.ConfigR4.BaseUrl,
            string.Empty,
            string.Empty,
            out string redirectDestination).Should().BeTrue();

        redirectDestination.Should().NotBeNullOrEmpty();

        string authKey = redirectDestination.Substring(redirectDestination.IndexOf("key=") + 4);
        authKey.Should().NotBeNullOrEmpty();

        _fixture.AuthR4.TryGetAuthorization(
            _fixture.Name,
            authKey,
            out AuthorizationInfo auth).Should().BeTrue();

        auth.Should().NotBeNull();

        // get the redirect

        // set patient and practitioner
        auth.LaunchPatient = launchPatient;
        auth.LaunchPractitioner = launchPractitioner;

        // authorize all scopes
        foreach (string key in auth.Scopes.Keys)
        {
            auth.Scopes[key] = true;
        }

        // update auth
        _fixture.AuthR4.TryUpdateAuth(_fixture.Name, authKey, auth).Should().BeTrue();

        // try to exchange the auth code for a token
        _fixture.AuthR4.TryCreateSmartResponse(_fixture.Name, auth.AuthCode, out AuthorizationInfo.SmartResponse response).Should().BeTrue();

        response.Should().NotBeNull();

        if (response == null)
        {
            return;
        }

        response.AccessToken.Should().NotBeNullOrEmpty();
    }
}

/// <summary>An authentication test fixture.</summary>
public class AuthTestFixture
{
    /// <summary>(Immutable) The name.</summary>
    public readonly string Name = "r4";

    /// <summary>(Immutable) The configuration for FHIR R4.</summary>
    public TenantConfiguration ConfigR4 { get; set; }

    public Dictionary<string, TenantConfiguration> Tenants { get; set; }

    /// <summary>The FHIR store for FHIR R4.</summary>
    public ISmartAuthManager AuthR4 { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthTestFixture"/> class.
    /// </summary>
    public AuthTestFixture()
    {
        ConfigR4 = new()
        {
            FhirVersion = TenantConfiguration.SupportedFhirVersions.R4,
            ControllerName = Name,
            BaseUrl = "http://localhost:5826/fhir/r4",
            SmartRequired = true,
        };

        Tenants = new()
        {
            { Name, ConfigR4 }
        };

        AuthR4 = new SmartAuthManager(
            Tenants, 
            new ServerConfiguration()
            {
                PublicUrl = "http://localhost:5826/fhir/r4",
                ListenPort = 5826,
                OpenBrowser = false,
                TenantsR4 = new() { Name },
                SmartRequiredTenants = new() { Name },
            },
            null);

        AuthR4.Init();
    }
}