// <copyright file="SmartAuthManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using fhir.candle.Models;
using fhir.candle.Pages.Subscriptions;
using FhirCandle.Models;
using FhirCandle.Storage;
using FhirStore.Smart;
using Hl7.Fhir.Rest;
using Hl7.Fhir.Support;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Asn1.Ocsp;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace fhir.candle.Services;

/// <summary>Manager for smart authentications.</summary>
public class SmartAuthManager : ISmartAuthManager, IDisposable
{
    /// <summary>(Immutable) The jwt signing value.</summary>
    private const string _jwtSign = "***NotSecure!DoNotUseInProduction!ThisIsForDevOnly!***";

    /// <summary>(Immutable) The token expiration in minutes.</summary>
    private const int _tokenExpirationMinutes = 30;
    
    /// <summary>(Immutable) The jwt signing value in bytes.</summary>
    private static readonly byte[] _jwtBytes = System.Text.Encoding.UTF8.GetBytes(_jwtSign);

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
    private Dictionary<string, SmartWellKnown> _smartConfigs = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The authorizations.</summary>
    private Dictionary<string, AuthorizationInfo> _authorizations = new();

    /// <summary>
    /// Initializes a new instance of the fhir.candle.Services.SmartAuthManager class.
    /// </summary>
    /// <param name="tenants">            The tenants.</param>
    /// <param name="serverConfiguration">The server configuration.</param>
    /// <param name="logger">             The logger.</param>
    public SmartAuthManager(
        Dictionary<string, TenantConfiguration> tenants,
        ServerConfiguration serverConfiguration,
        ILogger<SmartAuthManager>? logger)
    {
        _tenants = tenants;
        _serverConfig = serverConfiguration;
        _logger = logger ?? NullLoggerFactory.Instance.CreateLogger<SmartAuthManager>();
    }

    /// <summary>Gets the smart configuration by tenant.</summary>
    public Dictionary<string, SmartWellKnown> SmartConfigurationByTenant { get => _smartConfigs; }

    /// <summary>Gets the smart authorizations.</summary>
    public Dictionary<string, AuthorizationInfo> SmartAuthorizations { get => _authorizations; }

    /// <summary>Query if 'tenant' has tenant.</summary>
    /// <param name="tenant">The tenant.</param>
    /// <returns>True if tenant, false if not.</returns>
    public bool HasTenant(string tenant)
    {
        return _tenants.ContainsKey(tenant);
    }

    /// <summary>Attempts to get authorization.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="code">  The authorization code.</param>
    /// <param name="auth">  [out] The authentication.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetAuthorization(string tenant, string code, out AuthorizationInfo auth)
    {
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
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
        return true;
    }

    /// <summary>Attempts to update authentication.</summary>
    /// <param name="tenant">The tenant name.</param>
    /// <param name="code">  The authorization code.</param>
    /// <param name="auth">  [out] The authentication.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryUpdateAuth(string tenant, string code, AuthorizationInfo auth)
    {
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
        {
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // update our last access
        auth.LastAccessed = DateTimeOffset.UtcNow;
        auth.Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenExpirationMinutes);

        _authorizations[key] = auth;
        return true;
    }

    /// <summary>Attempts to get the authorization client redirect URL.</summary>
    /// <param name="tenant">          The tenant name.</param>
    /// <param name="code">            The authorization code.</param>
    /// <param name="redirect">        [out] The redirect.</param>
    /// <param name="error">           (Optional) The error.</param>
    /// <param name="errorDescription">(Optional) Information describing the error.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetClientRedirect(
        string tenant, 
        string code, 
        out string redirect,
        string error = "",
        string errorDescription = "")
    {
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
        {
            redirect = string.Empty;
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            redirect = string.Empty;
            return false;
        }

        if (string.IsNullOrEmpty(_authorizations[key].RequestParameters.RedirectUri))
        {
            redirect = string.Empty;
            return false;
        }

        // update our last access
        _authorizations[key].LastAccessed = DateTimeOffset.UtcNow;
        _authorizations[key].Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenExpirationMinutes);

        string redirectUri = _authorizations[key].RequestParameters.RedirectUri;

        // check for an error state redirection
        if (!string.IsNullOrEmpty(error))
        {
            // use our key as the authorization code
            if (redirectUri.Contains('?'))
            {
                redirect = $"{redirectUri}&error={System.Web.HttpUtility.UrlEncode(error)}";
            }
            else
            {
                redirect = $"{redirectUri}?error={System.Web.HttpUtility.UrlEncode(error)}";
            }

            if (!string.IsNullOrEmpty(errorDescription))
            {
                redirect = redirect + $"&error_description={System.Web.HttpUtility.UrlEncode(errorDescription)}";
            }

            return true;
        }

        // use our key as the authorization code
        if (redirectUri.Contains('?'))
        {
            redirect = $"{redirectUri}&code={_authorizations[key].AuthCode}&state={_authorizations[key].RequestParameters.State}";
        }
        else
        {
            redirect = $"{redirectUri}?code={_authorizations[key].AuthCode}&state={_authorizations[key].RequestParameters.State}";
        }

        return true;
    }

    /// <summary>Attempts to exchange a refresh token for a new access token.</summary>
    /// <param name="tenant">      The tenant.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <param name="clientId">    The client's identifier.</param>
    /// <param name="response">    [out] The response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TrySmartRefresh(
        string tenant,
        string refreshToken,
        string clientId,
        out AuthorizationInfo.SmartResponse response)
    {
        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("TrySmartRefresh <<< request is missing refresh token.");
            response = null!;
            return false;
        }

        if (refreshToken.Length < 36)
        {
            _logger.LogWarning($"TrySmartRefresh <<< request {refreshToken} is malformed.");
            response = null!;
            return false;
        }

        string code = refreshToken.Substring(0, 36);
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
        {
            _logger.LogWarning($"TrySmartRefresh <<< auth {key} does not exist.");
            response = null!;
            return false;
        }

        if (string.IsNullOrEmpty(tenant))
        {
            string msg = $"TrySmartRefresh <<< refresh of {refreshToken} is missing the tenant.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (string.IsNullOrEmpty(clientId))
        {
            string msg = $"TrySmartRefresh <<< refresh of {refreshToken} is missing the client id.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            string msg = $"TrySmartRefresh <<< {key} tenant ({local.Tenant}) does not match request: {tenant}.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (!clientId.Equals(local.RequestParameters.ClientId, StringComparison.Ordinal))
        {
            string msg = $"TrySmartRefresh <<< {key} client ({local.RequestParameters.ClientId}) does not match request: {clientId}.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (local.Response == null)
        {
            string msg = $"TrySmartRefresh <<< {key} does not have an issued refresh token.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (!refreshToken.Equals(local.Response.RefreshToken, StringComparison.Ordinal))
        {
            string msg = $"TrySmartRefresh <<< {key} refresh token {refreshToken} does not match issued: {local.Response.RefreshToken}.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        // handle our 'always on' token
        if (code.Equals(Guid.Empty.ToString()))
        {
            // update our last access
            local.LastAccessed = DateTimeOffset.UtcNow;
        }
        else
        {
            // update our last access and expiration
            local.LastAccessed = DateTimeOffset.UtcNow;
            local.Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenExpirationMinutes);

            // update the access and refresh tokens
            local.Response = local.Response with
            {
                AccessToken = code + "_" + Guid.NewGuid().ToString(),
                RefreshToken = code + "_" + Guid.NewGuid().ToString(),
            };
        }

        local.Activity.Add(new()
        {
            RequestType = "refresh_token",
            Success = true,
            Message = $"Refreshed access: {local.Response.AccessToken}, refresh token: {local.Response.RefreshToken}"
        });

        response = local.Response!;
        return true;
    }

    /// <summary>Query if this request is authorized.</summary>
    /// <param name="tenant">         The tenant.</param>
    /// <param name="accessToken">    The access token.</param>
    /// <param name="httpMethod">     The HTTP method.</param>
    /// <param name="interaction">    The interaction.</param>
    /// <param name="resourceType">   Type of the resource.</param>
    /// <param name="operationName">  Name of the operation.</param>
    /// <param name="compartmentType">Type of the compartment.</param>
    /// <returns>True if authorized, false if not.</returns>
    public bool IsAuthorized(
        string tenant,
        string accessToken,
        string httpMethod,
        Common.StoreInteractionCodes interaction,
        string resourceType,
        string operationName,
        string compartmentType)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogWarning("IsAuthorized <<< request is missing access token.");
            return false;
        }

        if (accessToken.Length < 36)
        {
            _logger.LogWarning($"IsAuthorized <<< request {accessToken} is malformed.");
            return false;
        }

        if (accessToken.Equals(Guid.Empty.ToString() + "_" + Guid.Empty.ToString()))
        {
            return true;
        }

        string code = accessToken.Substring(0, 36);
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
        {
            _logger.LogWarning($"IsAuthorized <<< auth {key} does not exist.");
            return false;
        }

        if (string.IsNullOrEmpty(tenant))
        {
            _logger.LogWarning("IsAuthorized <<< request is missing the tenant.");
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            string msg = $"TrySmartRefresh <<< {key} tenant ({local.Tenant}) does not match request: {tenant}.";
            local.Activity.Add(new()
            {
                RequestType = "refresh_token",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            return false;
        }

        if (local.UserScopes.Contains("*.*"))
        {
            return true;
        }

        switch (interaction)
        {
            // TODO: compartments are not implemented yet
            case Common.StoreInteractionCodes.CompartmentOperation:
            case Common.StoreInteractionCodes.CompartmentSearch:
            case Common.StoreInteractionCodes.CompartmentTypeSearch:
                break;

            case Common.StoreInteractionCodes.InstanceDelete:
            case Common.StoreInteractionCodes.InstanceDeleteHistory:
            case Common.StoreInteractionCodes.InstanceDeleteVersion:
            case Common.StoreInteractionCodes.TypeDeleteConditional:
                {
                    if (local.PatientScopes.Contains("*.d") ||
                        local.UserScopes.Contains("*.d") ||
                        local.PatientScopes.Contains(resourceType + ".d") ||
                        local.UserScopes.Contains(resourceType + ".d"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.InstanceOperation:
            case Common.StoreInteractionCodes.TypeOperation:
                {
                    switch (httpMethod.ToUpperInvariant())
                    {
                        case "HEAD":
                        case "GET":
                            {
                                if (local.PatientScopes.Contains("*.r") ||
                                    local.UserScopes.Contains("*.r") ||
                                    local.PatientScopes.Contains(resourceType + ".r") ||
                                    local.UserScopes.Contains(resourceType + ".r"))
                                {
                                    return true;
                                }

                                return false;
                            }

                        case "POST":
                        case "PUT":
                            {
                                if (local.PatientScopes.Contains("*.u") ||
                                    local.UserScopes.Contains("*.u") ||
                                    local.PatientScopes.Contains(resourceType + ".u") ||
                                    local.UserScopes.Contains(resourceType + ".u"))
                                {
                                    return true;
                                }

                                return false;
                            }
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.InstancePatch:
            case Common.StoreInteractionCodes.InstanceUpdate:
                {
                    if (local.PatientScopes.Contains("*.u") ||
                        local.UserScopes.Contains("*.u") ||
                        local.PatientScopes.Contains(resourceType + ".u") ||
                        local.UserScopes.Contains(resourceType + ".u"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.InstanceRead:
            case Common.StoreInteractionCodes.InstanceReadHistory:
            case Common.StoreInteractionCodes.InstanceReadVersion:
            case Common.StoreInteractionCodes.TypeHistory:
                {
                    if (local.PatientScopes.Contains("*.r") ||
                        local.UserScopes.Contains("*.r") ||
                        local.PatientScopes.Contains(resourceType + ".r") ||
                        local.UserScopes.Contains(resourceType + ".r"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.TypeSearch:
                {
                    if (local.PatientScopes.Contains("*.s") ||
                        local.UserScopes.Contains("*.s") ||
                        local.PatientScopes.Contains(resourceType + ".s") ||
                        local.UserScopes.Contains(resourceType + ".s"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.TypeCreate:
            case Common.StoreInteractionCodes.TypeCreateConditional:
                {
                    if (local.PatientScopes.Contains("*.c") ||
                        local.UserScopes.Contains("*.c") ||
                        local.PatientScopes.Contains(resourceType + ".c") ||
                        local.UserScopes.Contains(resourceType + ".c"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.SystemCapabilities:
                {
                    // always allow capabilities test
                    return true;
                }

            case Common.StoreInteractionCodes.SystemBundle:
                {
                    // only allow system bundles for user/*.*, which has already been checked
                    return false;
                }

            case Common.StoreInteractionCodes.SystemDeleteConditional:
                {
                    if (local.PatientScopes.Contains("*.d") ||
                        local.UserScopes.Contains("*.d"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.SystemHistory:
                {
                    if (local.PatientScopes.Contains("*.r") ||
                        local.UserScopes.Contains("*.r"))
                    {
                        return true;
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.SystemOperation:
                {
                    switch (httpMethod.ToUpperInvariant())
                    {
                        case "HEAD":
                        case "GET":
                            {
                                if (local.PatientScopes.Contains("*.r") ||
                                    local.UserScopes.Contains("*.r"))
                                {
                                    return true;
                                }

                                return false;
                            }

                        case "POST":
                        case "PUT":
                            {
                                if (local.PatientScopes.Contains("*.u") ||
                                    local.UserScopes.Contains("*.u"))
                                {
                                    return true;
                                }

                                return false;
                            }
                    }

                    return false;
                }

            case Common.StoreInteractionCodes.SystemSearch:
                {
                    if (local.PatientScopes.Contains("*.s") ||
                        local.UserScopes.Contains("*.s"))
                    {
                        return true;
                    }

                    return false;
                }

            default:
                break;
        }

        return false;
    }

    /// <summary>Attempts to create smart response.</summary>
    /// <param name="tenant">      The tenant name.</param>
    /// <param name="authCode">    The authorization code.</param>
    /// <param name="clientId">    The client's identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <param name="codeVerifier">The code verifier.</param>
    /// <param name="response">    [out] The response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryCreateSmartResponse(
        string tenant,
        string authCode,
        string clientId,
        string clientSecret,
        string codeVerifier,
        out AuthorizationInfo.SmartResponse response)
    {
        if (string.IsNullOrEmpty(authCode))
        {
            _logger.LogWarning("TryCreateSmartResponse <<< request is missing authorization code.");
            response = null!;
            return false;
        }

        if (authCode.Length < 36)
        {
            _logger.LogWarning($"TryCreateSmartResponse <<< request {authCode} is malformed.");
            response = null!;
            return false;
        }

        string code = authCode.Substring(0, 36);
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
        {
            _logger.LogWarning($"TryCreateSmartResponse <<< auth {key} does not exist.");
            response = null!;
            return false;
        }

        if (string.IsNullOrEmpty(tenant))
        {
            string msg = $"TryCreateSmartResponse <<< request {authCode} is missing the tenant.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (string.IsNullOrEmpty(clientId))
        {
            string msg = $"TryCreateSmartResponse <<< request {authCode} is missing the client id.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            string msg = $"TryCreateSmartResponse <<< {key} tenant ({local.Tenant}) does not match request: {tenant}.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (!clientId.Equals(local.RequestParameters.ClientId, StringComparison.Ordinal))
        {
            string msg = $"TryCreateSmartResponse <<< {key} client ({local.RequestParameters.ClientId}) does not match request: {clientId}.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        // check the PKCE code if one has been provided
        if (!string.IsNullOrEmpty(local.RequestParameters.PkceChallenge))
        {
            if (string.IsNullOrEmpty(codeVerifier))
            {
                string msg = $"TryCreateSmartResponse <<< code verifier is required if initial request contains PKCE!";
                local.Activity.Add(new()
                {
                    RequestType = "authorization_code",
                    Success = false,
                    Message = msg,
                });
                _logger.LogWarning(msg);
                response = null!;
                return false;
            }

            string coded = string.Empty;

            using (System.Security.Cryptography.SHA256 s256 = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = s256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
                coded = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(hash);
            }

            if (!coded.Equals(local.RequestParameters.PkceChallenge, StringComparison.Ordinal))
            {
                string msg = $"TryCreateSmartResponse <<< code verifier does not match PKCE challenge!";
                local.Activity.Add(new()
                {
                    RequestType = "authorization_code",
                    Success = false,
                    Message = msg,
                });
                _logger.LogWarning(msg);
                response = null!;
                return false;
            }
        }

        IEnumerable<string> permittedScopes = local.Scopes.Where(kvp => kvp.Value == true).Select(kvp => kvp.Key);

        ExtractScopes(permittedScopes, out HashSet<string> userScopes, out HashSet<string> patientScopes);
        local.UserScopes = userScopes;
        local.PatientScopes = patientScopes;

        // check for 'special' code
        if (code.Equals(Guid.Empty.ToString()))
        {
            // update our last access
            local.LastAccessed = DateTimeOffset.UtcNow;

            // create our response
            local.Response = new()
            {
                PatientId = local.LaunchPatient,
                TokenType = "bearer",
                Scopes = string.Join(" ", permittedScopes),
                ClientId = local.RequestParameters.ClientId,
                IdToken = GenerateIdJwt(_tenants[tenant].BaseUrl, local),
                AccessToken = code + "_" + code,
                RefreshToken = code + "_" + code,
            };
        }
        else
        {
            // update our last access and expiration
            local.LastAccessed = DateTimeOffset.UtcNow;
            local.Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenExpirationMinutes);

            // create our response
            local.Response = new()
            {
                PatientId = local.LaunchPatient,
                TokenType = "bearer",
                Scopes = string.Join(" ", permittedScopes),
                ClientId = local.RequestParameters.ClientId,
                IdToken = GenerateIdJwt(_tenants[tenant].BaseUrl, local),
                AccessToken = code + "_" + Guid.NewGuid().ToString(),    // GenerateAccessJwt(_tenants[tenant].BaseUrl, local),
                RefreshToken = code + "_" + Guid.NewGuid().ToString()
            };
        }


        local.Activity.Add(new()
        {
            RequestType = "authorization_code",
            Success = true,
            Message = $"Granted access token: {local.Response.AccessToken}, refresh token: {local.Response.RefreshToken}"
        });

        response = local.Response!;
        return true;
    }

    /// <summary>Extracts the scopes.</summary>
    /// <param name="scopes">       The scopes.</param>
    /// <param name="userScopes">   [out] The user scopes.</param>
    /// <param name="patientScopes">[out] The patient scopes.</param>
    private void ExtractScopes(
        IEnumerable<string> scopes,
        out HashSet<string> userScopes, 
        out HashSet<string> patientScopes)
    {
        userScopes = new();
        patientScopes = new();

        // normalize our allowed scopes
        foreach (string scope in scopes)
        {
            // scopes we care about are [context]/[resource].[action][?granular]
            string[] components = scope.Split('/', '.', '?');

            // we do not care about scopes that do not match our pattern
            if (components.Length < 3)
            {
                continue;
            }

            switch (components[0])
            {
                case "user":
                    AddScope(components[1], components[2].ToLowerInvariant(), ref userScopes);
                    break;

                case "patient":
                    AddScope(components[1], components[2].ToLowerInvariant(), ref patientScopes);
                    break;
            }
        }

        void AddScope(string resource, string actions, ref HashSet<string> scopeSet)
        {
            if (string.IsNullOrEmpty(resource) || string.IsNullOrEmpty(actions))
            {
                return;
            }

            // check for v1 scopes and all (*)
            switch (actions)
            {
                case "read":
                    {
                        scopeSet.Add(resource + ".r");
                        scopeSet.Add(resource + ".s");
                        return;
                    }

                case "write":
                    {
                        scopeSet.Add(resource + ".c");
                        scopeSet.Add(resource + ".u");
                        scopeSet.Add(resource + ".d");
                        return;
                    }

                case "*":
                    {
                        scopeSet.Add(resource + ".c");
                        scopeSet.Add(resource + ".r");
                        scopeSet.Add(resource + ".u");
                        scopeSet.Add(resource + ".d");
                        scopeSet.Add(resource + ".s");
                        return;
                    }
            }

            // v2 scopes can be in any order
            if (actions.Contains('c'))
            {
                scopeSet.Add(resource + ".c");
            }

            if (actions.Contains('r'))
            {
                scopeSet.Add(resource + ".r");
            }

            if (actions.Contains('u'))
            {
                scopeSet.Add(resource + ".u");
            }

            if (actions.Contains('d'))
            {
                scopeSet.Add(resource + ".d");
            }

            if (actions.Contains('s'))
            {
                scopeSet.Add(resource + ".s");
            }
        }
    }

    /// <summary>Generates an access-token jwt.</summary>
    /// <param name="rootUrl">URL of the root.</param>
    /// <param name="auth">   [out] The authentication.</param>
    /// <returns>The jwt.</returns>
    internal string GenerateAccessJwt(string rootUrl, AuthorizationInfo auth)
    {
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new System.Security.Claims.ClaimsIdentity(new System.Security.Claims.Claim[]
            {
                new("sub", auth.UserId.GetHashCode().ToString()),
                new("jti", Guid.NewGuid().ToString()),
                //new("aud", auth.RequestParameters.Audience),
                //new("iss", rootUrl),
                //new("exp", auth.Expires.ToUnixTimeSeconds().ToString()),
                //new("iat", auth.Created.ToUnixTimeSeconds().ToString()),
            }),
            Expires = auth.Expires.DateTime,
            Audience = auth.RequestParameters.Audience,
            Issuer = rootUrl,
            IssuedAt = auth.LastAccessed.DateTime,
            SigningCredentials = new(new SymmetricSecurityKey(_jwtBytes), SecurityAlgorithms.HmacSha256Signature),
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    /// <summary>Attempts to introspection.</summary>
    /// <param name="tenant">  The tenant.</param>
    /// <param name="token">   The token.</param>
    /// <param name="response">[out] The response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryIntrospection(
        string tenant,
        string token,
        out AuthorizationInfo.IntrospectionResponse? response)
    {
        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("TryIntrospection <<< request is missing token.");
            response = null!;
            return false;
        }

        if (token.Length < 36)
        {
            _logger.LogWarning($"TryIntrospection <<< request {token} is malformed.");
            response = null;
            return false;
        }

        string code = token.Substring(0, 36);
        string key = tenant + ":" + code;

        if (!_authorizations.TryGetValue(key, out AuthorizationInfo? local))
        {
            _logger.LogWarning($"TryIntrospection <<< auth {key} was not found.");
            response = null!;
            return false;
        }

        if (string.IsNullOrEmpty(tenant))
        {
            string msg = $"TryIntrospection <<< request {token} is missing the tenant.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (!local.Tenant.Equals(tenant, StringComparison.OrdinalIgnoreCase))
        {
            string msg = $"TryIntrospection <<< {key} tenant ({local.Tenant}) does not match request: {tenant}.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null!;
            return false;
        }

        if (local.Response == null)
        {
            string msg = $"TryIntrospection <<< {key} has not retrieved an access token.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null;
            return false;
        }

        if (!token.Equals(local.Response.AccessToken, StringComparison.Ordinal))
        {
            string msg = $"TryIntrospection <<< {key} access token ({local.Response.AccessToken}) does not match request: {token}.";
            local.Activity.Add(new()
            {
                RequestType = "authorization_code",
                Success = false,
                Message = msg,
            });
            _logger.LogWarning(msg);
            response = null;
            return false;
        }

        response = new()
        {
            Active = true,
            Scopes = string.Join(' ', local.Scopes.Where(kvp => kvp.Value == true).Select(kvp => kvp.Key)),
            ClientId = local.RequestParameters.ClientId,
            Username = local.UserId,
            Subject = local.UserId.GetHashCode().ToString(),
            Audience = local.RequestParameters.Audience,
            ExpiresAt = local.Expires.ToUnixTimeSeconds(),
            IssuedAt = local.LastAccessed.ToUnixTimeSeconds(),
        };

        return true;
    }

    /// <summary>Generates an id-token jwt.</summary>
    /// <param name="rootUrl">URL of the root.</param>
    /// <param name="auth">   [out] The authentication.</param>
    /// <returns>The identifier jwt.</returns>
    internal string GenerateIdJwt(string rootUrl, AuthorizationInfo auth)
    {
        JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
        SecurityTokenDescriptor tokenDescriptor = new()
        {
            Subject = new System.Security.Claims.ClaimsIdentity(new System.Security.Claims.Claim[]
            {
                new("sub", auth.Key + "_" + Guid.NewGuid().ToString()),
                new("profile", auth.UserId),
                new("fhirUser", auth.UserId),
                new("jti", Guid.NewGuid().ToString()),
            }),
            Expires = auth.Expires.DateTime,
            Audience = auth.RequestParameters.Audience,
            Issuer = rootUrl,
            IssuedAt = auth.LastAccessed.DateTime,
            SigningCredentials = new(new SymmetricSecurityKey(_jwtBytes), SecurityAlgorithms.HmacSha256Signature),
        };

        SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
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
                //TokenEndpointAuthSigningAlgs = new string[]
                //{
                //    //"RS384",
                //    //"ES384",
                //},
                //RegistrationEndpoint = $"{config.BaseUrl}/auth/register",
                //AppStateEndpoint = $"{config.BaseUrl}/auth/appstate",
                SupportedScopes = new string[]
                {
                    //"openid",
                    "profile",
                    //"offline_access",
                    "fhirUser",
                    "launch",
                    "launch/patient",
                    //"launch/practitioner",
                    //"launch/encounter",
                    //"patient/*.read",
                    //"patient/*.r",
                    "patient/*.*",
                    //"user/*.read",
                    //"user/*.rs",
                    "user/*.*",
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
                IntrospectionEndpoint = $"{_serverConfig.PublicUrl}/_smart/{name}/introspect",
                //RecovationEndpoint = $"{config.BaseUrl}/auth/revoke",
                Capabilities = new string[]
                {
                        //"launch-ehr",                             // SMART's EHR Launch mode
                        "launch-standalone",                        // SMART's Standalone Launch mode
                        //"authorize-post",                         // POST-based authorization
                        "client-public",                            // SMART's public client profile (no client authentication)
                        "client-confidential-symmetric",            // SMART's symmetric confidential client profile ("client secret" authentication)
                        //"client-confidential-asymmetric",         // SMART's asymmetric confidential client profile ("JWT authentication")
                        //"sso-openid-connect",                     // SMART's OpenID Connect profile
                        //"context-banner",                         // "need patient banner" launch context (conveyed via need_patient_banner token parameter)
                        //"context-style",                          // "SMART style URL" launch context (conveyed via smart_style_url token parameter). This capability is deemed experimental.
                        //"context-ehr-patient",                    // patient-level launch context (requested by launch/patient scope, conveyed via patient token parameter)
                        //"context-ehr-encounter",                  // encounter-level launch context (requested by launch/encounter scope, conveyed via encounter token parameter)
                        "context-standalone-patient",               // patient-level launch context (requested by launch/patient scope, conveyed via patient token parameter)
                        //"context-standalone-encounter",           // encounter-level launch context (requested by launch/encounter scope, conveyed via encounter token parameter)
                        //"permission-offline",                     // refresh tokens (requested by offline_access scope)
                        //"permission-online",                      // refresh tokens (requested by online_access scope)
                        "permission-patient",                       // patient-level scopes (e.g., patient/Observation.rs)
                        "permission-user",                          // user-level scopes (e.g., user/Appointment.rs)
                        "permission-v1",                            // SMARTv1 scope syntax (e.g., patient/Observation.read)
                        "permission-v2",                            // SMARTv2 granular scope syntax (e.g., patient/Observation.rs?...)
                                                                    //"smart-app-state",                    // managing SMART App State - experimental
                },
                SupportedChallengeMethods = new string[]
                {
                        "S256",
                },
            });

            // create our 'always available' authorization
            AuthorizationInfo auth = new()
            {
                Key = Guid.Empty.ToString(),
                Tenant = name,
                RemoteIpAddress = "127.0.0.1",
                Created = DateTimeOffset.UtcNow,
                LastAccessed = DateTimeOffset.UtcNow,
                Expires = DateTimeOffset.MaxValue,
                UserId = "administrator",
                RequestParameters = new()
                {
                    ResponseType = "code",
                    ClientId = "fhir-candle",
                    RedirectUri = string.Empty,
                    Scope = "fhirUser profile user/*.*",
                    Audience = $"{_serverConfig.PublicUrl}/fhir/{name}",
                },
                AuthCode = Guid.Empty.ToString() + "_" + Guid.Empty.ToString(),
            };

            foreach (string scopeKey in auth.Scopes.Keys)
            {
                auth.Scopes[scopeKey] = true;
            }

            auth.UserScopes.Add("*.*");

            auth.Response = new()
            {
                TokenType = "bearer",
                Scopes = "fhirUser profile user/*.*",
                ClientId = "fhir-candle",
                IdToken = GenerateIdJwt(auth.RequestParameters.Audience, auth),
                AccessToken = Guid.Empty.ToString() + "_" + Guid.Empty.ToString(),
                RefreshToken = Guid.Empty.ToString() + "_" + Guid.Empty.ToString(),
            };

            _authorizations.Add(name + ":" + Guid.Empty.ToString(), auth);
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
    /// <param name="authKey">            [out] The authentication key.</param>
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
        out string redirectDestination,
        out string authKey)
    {
        if (!_smartConfigs.ContainsKey(tenant))
        {
            redirectDestination = string.Empty;
            authKey = string.Empty;
            return false;
        }

        // check our audience
        if (!audience.Equals(_tenants[tenant].BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            if (audience.EndsWith('/') && !_tenants[tenant].BaseUrl.EndsWith('/'))
            {
                if (!audience.Equals(_tenants[tenant].BaseUrl + "/", StringComparison.OrdinalIgnoreCase))
                {
                    redirectDestination = string.Empty;
                    authKey = string.Empty;
                    return false;
                }
            }
            else if (_tenants[tenant].BaseUrl.EndsWith('/') && !audience.EndsWith('/'))
            {
                if (!audience.Equals(_tenants[tenant].BaseUrl.Substring(0, _tenants[tenant].BaseUrl.Length - 1), StringComparison.OrdinalIgnoreCase))
                {
                    redirectDestination = string.Empty;
                    authKey = string.Empty;
                    return false;
                }
            }
            else
            {
                redirectDestination = string.Empty;
                authKey = string.Empty;
                return false;
            }
        }

        // create our auth
        AuthorizationInfo auth = new()
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
            Expires = DateTimeOffset.UtcNow.AddMinutes(_tokenExpirationMinutes),
        };

        auth.AuthCode = auth.Key + "_" + Guid.NewGuid().ToString();

        _authorizations.Add(tenant + ":" + auth.Key, auth);

        redirectDestination = $"/smart/login?store={tenant}&key={auth.Key}";
        authKey = auth.Key;
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
