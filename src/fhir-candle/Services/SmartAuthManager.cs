// <copyright file="SmartAuthManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Models;
using FhirCandle.Models;
using FhirStore.Smart;
using Org.BouncyCastle.Asn1.Ocsp;

namespace fhir.candle.Services;

/// <summary>Manager for smart authentications.</summary>
public class SmartAuthManager : ISmartAuthManager, IDisposable
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>True if is initialized, false if not.</summary>
    private bool _isInitialized = false;

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>The tenants.</summary>
    private Dictionary<string, TenantConfiguration> _tenants;

    /// <summary>The server configuration.</summary>
    private ServerConfiguration _serverConfig;

    /// <summary>The smart configs.</summary>
    private Dictionary<string, SmartWellKnown> _smartConfigs = new();

    /// <summary>The authorizations.</summary>
    private Dictionary<string, SmartAuthorization> _authorizations = new();

    /// <summary>The token key map.</summary>
    private Dictionary<string, string> _tokenKeyMap = new();

    public SmartAuthManager(
        Dictionary<string, TenantConfiguration> tenants,
        ILogger<SmartAuthManager> logger,
        ServerConfiguration serverConfiguration)
    {
        _tenants = tenants;
        _logger = logger;
        _serverConfig = serverConfiguration;
    }

    /// <summary>Gets the smart configuration by tenant.</summary>
    public Dictionary<string, SmartWellKnown> SmartConfigurationByTenant { get => _smartConfigs; }

    /// <summary>Gets the smart authorizations.</summary>
    public Dictionary<string, SmartAuthorization> SmartAuthorizations { get => _authorizations; }

    /// <summary>Query if 'tenant' has tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>True if tenant, false if not.</returns>
    public bool HasTenant(string tenant)
    {
        return _tenants.ContainsKey(tenant);
    }

    /// <summary>Attempts to get authorization.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="key">   The key.</param>
    /// <param name="auth">  [out] The authentication.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetAuthorization(string tenant, string key, out SmartAuthorization auth)
    {
        if (!_authorizations.TryGetValue(key, out SmartAuthorization? local))
        {
            auth = null!;
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            auth = null!;
            return false;
        }

        auth = local;
        return false;
    }


    /// <summary>Initializes this service.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    public void Init()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;

        _logger.LogInformation("SmartAuthManager <<< Creating FHIR tenants...");

        // initialize the requested fhir stores
        foreach ((string name, TenantConfiguration config) in _tenants)
        {
            // build smart config
            if (!config.SmartRequired && !config.SmartAllowed)
            {
                continue;
            }

            _smartConfigs.Add(name, new()
            {
                GrantTypes = new string[]
                {
                        "authorization_code",
                },
                AuthorizationEndpoint = $"{_serverConfig.PublicUrl}/_smart/{name}/authorize",
                TokenEndpoint = $"{_serverConfig.PublicUrl}/_smart/{name}/token",
                TokenEndpointAuthMethods = new string[]
                {
                        //"client_secret_post",
                        "client_secret_basic",
                    //"private_key_jwt",
                },
                //RegistrationEndpoint = $"{config.BaseUrl}/auth/register",
                //AppStateEndpoint = $"{config.BaseUrl}/auth/appstate",
                SupportedScopes = new string[]
                {
                        "launch",
                        "launch/patient",
                        //"patient/*.read",
                        //"patient/*.r",
                        //"patient/*.*",
                        "user/*.read",
                        "user/*.rs",
                        "user/*.*",
                        "openid",
                        "fhirUser",
                    //"profile",
                    //"offline_access",
                },
                SupportedResponseTypes = new string[]
                {
                        "code",                     // Authorization Code Flow
                        "id_token",                 // Implicit Flow
                        //"id_token token",         // Implicit Flow
                        "code id_token",            // Hybrid Flow
                        //"code token",             // Hybrid Flow
                        //"code token id_token",    // Hybrid Flow
                        "refresh_token",
                },
                //ManagementEndpoint = $"{config.BaseUrl}/auth/manage",
                //IntrospectionEndpoint = $"{config.BaseUrl}/auth/introspect",
                //RecovationEndpoint = $"{config.BaseUrl}/auth/revoke",
                Capabilities = new string[]
                {
                        //"launch-ehr",                         // SMART's EHR Launch mode
                        "launch-standalone",                    // SMART's Standalone Launch mode
                        //"authorize-post",                     // POST-based authorization
                        "client-public",                        // SMART's public client profile (no client authentication)
                        //"client-confidential-symmetric",      // SMART's symmetric confidential client profile ("client secret" authentication)
                        //"client-confidential-asymmetric",     // SMART's asymmetric confidential client profile ("JWT authentication")
                        //"sso-openid-connect",                 // SMART's OpenID Connect profile
                        //"context-banner",                     // "need patient banner" launch context (conveyed via need_patient_banner token parameter)
                        //"context-style",                      // "SMART style URL" launch context (conveyed via smart_style_url token parameter). This capability is deemed experimental.
                        //"context-ehr-patient",                // patient-level launch context (requested by launch/patient scope, conveyed via patient token parameter)
                        //"context-ehr-encounter",              // encounter-level launch context (requested by launch/encounter scope, conveyed via encounter token parameter)
                        "context-standalone-patient",           // patient-level launch context (requested by launch/patient scope, conveyed via patient token parameter)
                        //"context-standalone-encounter",       // encounter-level launch context (requested by launch/encounter scope, conveyed via encounter token parameter)
                        //"permission-offline",                 // refresh tokens (requested by offline_access scope)
                        //"permission-online",                  // refresh tokens (requested by online_access scope)
                        //"permission-patient",                 // patient-level scopes (e.g., patient/Observation.rs)
                        "permission-user",                      // user-level scopes (e.g., user/Appointment.rs)
                        "permission-v1",                        // SMARTv1 scope syntax (e.g., patient/Observation.read)
                        "permission-v2",                        // SMARTv2 granular scope syntax (e.g., patient/Observation.rs?...)
                                                                //"smart-app-state",                    // managing SMART App State - experimental
                },
                SupportedChallengeMethods = new string[]
                {
                        "S256",
                },
            });
        }

        // look for preconfigured users
        //string root =
        //    Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ??
        //    Environment.CurrentDirectory ??
        //    string.Empty;

        //if (!string.IsNullOrEmpty(_serverConfig.ReferenceImplementation))
        //{
        //    // look for a package supplemental directory
        //    string supplemental = string.IsNullOrEmpty(_serverConfig.SourceDirectory)
        //        ? Program.FindRelativeDir(root, Path.Combine("fhirData", _serverConfig.ReferenceImplementation), false)
        //        : Path.Combine(_serverConfig.SourceDirectory, _serverConfig.ReferenceImplementation);

        //    LoadRiContents(supplemental);
        //}
    }

    /// <summary>Request authentication.</summary>
    /// <param name="tenant">             The tenant.</param>
    /// <param name="remoteIpAddress">    The remote IP address.</param>
    /// <param name="responseType">       Fixed value: code.</param>
    /// <param name="clientId">           The client's identifier.</param>
    /// <param name="redirectUri">        Must match one of the client's pre-registered redirect URIs.</param>
    /// <param name="launch">             When using the EHR Launch flow, this must match the launch
    ///  value received from the EHR. Omitted when using the Standalone Launch.</param>
    /// <param name="scope">              Must describe the access that the app needs.</param>
    /// <param name="state">              An opaque value used by the client to maintain state between
    ///  the request and callback.</param>
    /// <param name="audience">           URL of the EHR resource server from which the app wishes to
    ///  retrieve FHIR data.</param>
    /// <param name="pkceChallenge">      This parameter is generated by the app and used for the code
    ///  challenge, as specified by PKCE. (required v2, opt v1)</param>
    /// <param name="pkceMethod">         Method used for the code_challenge parameter. (required v2,
    ///  opt v1)</param>
    /// <param name="redirectDestination">[out] The redirect destination.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool RequestAuth(
        string tenant,
        string remoteIpAddress,
        string responseType,
        string clientId,
        string redirectUri,
        string? launch,
        string scope,
        string state,
        string audience,
        string? pkceChallenge,
        string? pkceMethod,
        out string redirectDestination)
    {
        if (!_smartConfigs.ContainsKey(tenant))
        {
            redirectDestination = string.Empty;
            return false;
        }

        // create our auth - default to 5 minute timeout
        SmartAuthorization auth = new()
        {
            Key = Guid.NewGuid().ToString(),
            Tenant = tenant,
            RemoteIpAddress = remoteIpAddress,
            RequestParameters = new()
            {
                ResponseType = responseType,
                ClientId = clientId,
                RedirectUri = redirectUri,
                Launch = launch,
                Scope = scope,
                State = state,
                Audience = audience,
                PkceChallenge = pkceChallenge,
                PkceMethod = pkceMethod,
            },
            Expires = DateTimeOffset.UtcNow.AddMinutes(5),
        };

        _authorizations.Add(auth.Key, auth);

        redirectDestination = $"/smart/login?store={tenant}&key={auth.Key}";

        return true;
    }


    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting SmartAuthManager...");

        Init();

        return Task.CompletedTask;
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Releases the unmanaged resources used by the
    /// FhirModelComparer.Server.Services.FhirManagerService and optionally releases the managed
    /// resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)

                //foreach (IFhirStore store in _storesByController.Values)
                //{
                //    store.OnSubscriptionSendEvent -= FhirStoreManager_OnSubscriptionSendEvent;
                //}
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _hasDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
