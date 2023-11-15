// <copyright file="FhirRequestContext.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Storage;
using System;
using System.Diagnostics.CodeAnalysis;

namespace FhirCandle.Models;

/// <summary>A FHIR request context.</summary>
public record class FhirRequestContext
{
    private Common.ParsedInteraction? _interaction = null;
    private string _url = string.Empty;
    private string _httpMethod = string.Empty;
    private IFhirStore _store = null!;

    /// <summary>Initializes a new instance of the <see cref="FhirRequestContext"/> class.</summary>
    public FhirRequestContext() { }

    /// <summary>Initializes a new instance of the <see cref="FhirRequestContext"/> class.</summary>
    /// <param name="store">     The store.</param>
    /// <param name="httpMethod">The HTTP method.</param>
    /// <param name="url">       URL of the resource.</param>
    [SetsRequiredMembers]
    public FhirRequestContext(
        IFhirStore store,
        string httpMethod,
        string url)
    {
        Store = store;
        TenantName = store.Config.ControllerName;
        HttpMethod = httpMethod;
        Url = url;
    }

    /// <summary>Initializes a new instance of the <see cref="FhirRequestContext"/> class.</summary>
    /// <param name="other">The other.</param>
    [SetsRequiredMembers]
    protected FhirRequestContext(FhirRequestContext other)
    {
        Store = other.Store;
        TenantName = other.TenantName;
        HttpMethod = other.HttpMethod;
        Url = other.Url;
        _interaction = other._interaction;
        Authorization = other.Authorization;
    }

    /// <summary>Gets or sets the name of the tenant.</summary>
    public required string TenantName { get; init; }

    /// <summary>Gets or sets the store.</summary>
    public required IFhirStore Store { get => _store; init => _store = value; }

    /// <summary>Gets or sets the HTTP method.</summary>
    public required string HttpMethod { get => _httpMethod; init => _httpMethod = value.ToUpperInvariant(); }

    /// <summary>Gets or sets URL of the document.</summary>
    public required string Url { get => _url; init => _url = value; }

    /// <summary>Gets or initializes the authorization.</summary>
    public required AuthorizationInfo? Authorization { get; init; }

    /// <summary>Gets or initializes source format.</summary>
    public string SourceFormat { get; init; } = string.Empty;
    
    /// <summary>Gets or initializes destination format (default to fhir+json).</summary>
    public string DestinationFormat { get; init; } = "application/fhir+json";

    /// <summary>Gets the interaction.</summary>
    /// <exception cref="Exception">Thrown when an exception error condition occurs.</exception>
    public Common.ParsedInteraction FhirInteraction
    {
        get
        {
            if (_interaction == null)
            {
                if (!TryParseRequest(out _interaction))
                {
                    if (_interaction == null)
                    {
                        throw new Exception($"Failed to parse: {_httpMethod} {_url}");
                    }
                }
            }

            return (Common.ParsedInteraction)_interaction!;
        }
        init
        {
            _interaction = value;
        }
    }

    /// <summary>Attempts to parse this request.</summary>
    /// <param name="result">[out] The result.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParseRequest(out Common.ParsedInteraction? parsed)
    {
        string errorMessage;
        string requestUrlPath;
        string requestUrlQuery;
        string resourceType = string.Empty;

        string[] pathAndQuery = _url.Split('?', StringSplitOptions.RemoveEmptyEntries);

        switch (pathAndQuery.Length)
        {
            case 0:
                requestUrlPath = string.Empty;
                requestUrlQuery = string.Empty;
                break;

            case 1:
                if (_url.StartsWith('?'))
                {
                    requestUrlPath = string.Empty;
                    requestUrlQuery = pathAndQuery[0];
                }
                else
                {
                    requestUrlPath = pathAndQuery[0];
                    requestUrlQuery = string.Empty;
                }
                break;

            case 2:
                requestUrlPath = pathAndQuery[0];
                requestUrlQuery = pathAndQuery[1];
                break;

            default:
                // assume there are query parameters that contain '?'
                requestUrlPath = pathAndQuery[0];
                requestUrlQuery = string.Join('?', pathAndQuery[1..]);
                break;
        }

        if (requestUrlPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            requestUrlPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            requestUrlPath = requestUrlPath.Substring(requestUrlPath.IndexOf(':'));
            string configUrl = _store.Config.BaseUrl.Substring(_store.Config.BaseUrl.IndexOf(':'));

            if (requestUrlPath.StartsWith(configUrl, StringComparison.OrdinalIgnoreCase))
            {
                requestUrlPath = requestUrlPath.Substring(configUrl.Length);
                if (requestUrlPath.StartsWith('/'))
                {
                    requestUrlPath = requestUrlPath.Substring(1);
                }
            }
            else
            {
                errorMessage = $"DetermineInteraction: Full URL: {_url} cannot be parsed!";
                Console.WriteLine(errorMessage);

                parsed = new()
                {
                    ErrorMessage = errorMessage
                };

                return false;
            }
        }

        string[] pathComponents = requestUrlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        bool hasValidResourceType = pathComponents.Any()
            ? _store.Keys.Contains(pathComponents[0])
            : false;

        if (hasValidResourceType)
        {
            resourceType = pathComponents[0];
        }

        switch (_httpMethod)
        {
            case "GET":
                {
                    switch (pathComponents.Length)
                    {
                        case 0:
                            {
                                parsed = new()
                                {
                                    HttpMehtod = _httpMethod,
                                    UrlPath = requestUrlPath,
                                    UrlQuery = requestUrlQuery,
                                    Interaction = Common.StoreInteractionCodes.SystemSearch,
                                };

                                return true;
                            }

                        case 1:
                            {
                                if (hasValidResourceType)
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Interaction = Common.StoreInteractionCodes.TypeSearch,
                                    };

                                    return true;
                                }

                                if (pathComponents[0].Equals("metadata", StringComparison.Ordinal))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        Interaction = Common.StoreInteractionCodes.SystemCapabilities,
                                    };

                                    return true;
                                }

                                if (pathComponents[0].Equals("_history", StringComparison.Ordinal))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        Interaction = Common.StoreInteractionCodes.SystemHistory,
                                    };

                                    return true;
                                }

                                if (pathComponents[0].StartsWith('$'))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        OperationName = pathComponents[0],
                                        Interaction = Common.StoreInteractionCodes.SystemOperation,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 2:
                            {
                                if (hasValidResourceType)
                                {
                                    if (pathComponents[1].StartsWith('$'))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            OperationName = pathComponents[1],
                                            Interaction = Common.StoreInteractionCodes.TypeOperation,
                                        };

                                        return true;
                                    }

                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Interaction = Common.StoreInteractionCodes.InstanceRead,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 3:
                            {
                                if (hasValidResourceType)
                                {
                                    if (pathComponents[2].StartsWith('$'))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Id = pathComponents[1],
                                            OperationName = pathComponents[2],
                                            Interaction = Common.StoreInteractionCodes.InstanceOperation,
                                        };

                                        return true;
                                    }

                                    if (pathComponents[2].Equals("_history", StringComparison.Ordinal))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Id = pathComponents[1],
                                            Interaction = Common.StoreInteractionCodes.InstanceReadHistory,
                                        };

                                        return true;
                                    }

                                    if (pathComponents[2].Equals("*", StringComparison.Ordinal))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Id = pathComponents[1],
                                            CompartmentType = pathComponents[2],
                                            Interaction = Common.StoreInteractionCodes.CompartmentSearch,
                                        };

                                        return true;
                                    }

                                    if (_store.Keys.Contains(pathComponents[2]))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Id = pathComponents[1],
                                            CompartmentType = pathComponents[2],
                                            Interaction = Common.StoreInteractionCodes.CompartmentTypeSearch,
                                        };

                                        return true;
                                    }
                                }
                            }
                            break;

                        case 4:
                            {
                                if (hasValidResourceType &&
                                    pathComponents[2].Equals("_history", StringComparison.Ordinal) &&
                                    (!pathComponents[3].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Version = pathComponents[3],
                                        Interaction = Common.StoreInteractionCodes.InstanceReadVersion,
                                    };

                                    return true;
                                }
                            }
                            break;
                    }
                }
                break;

            // head is allowed on a subset of GET requests - specifically cacheable reads (instance/capabilities)
            case "HEAD":
                {
                    switch (pathComponents.Length)
                    {
                        case 1:
                            {
                                if (pathComponents[0].Equals("metadata", StringComparison.Ordinal))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        Interaction = Common.StoreInteractionCodes.SystemCapabilities,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 2:
                            {
                                if (hasValidResourceType &&
                                    (!pathComponents[1].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Interaction = Common.StoreInteractionCodes.InstanceRead,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 4:
                            {
                                if (hasValidResourceType &&
                                    pathComponents[2].Equals("_history", StringComparison.Ordinal) &&
                                    (!pathComponents[3].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Version = pathComponents[3],
                                        Interaction = Common.StoreInteractionCodes.InstanceReadVersion,
                                    };

                                    return true;
                                }
                            }
                            break;
                    }
                }
                break;


            case "POST":
                {
                    switch (pathComponents.Length)
                    {
                        case 0:
                            {
                                parsed = new()
                                {
                                    HttpMehtod = _httpMethod,
                                    UrlPath = requestUrlPath,
                                    UrlQuery = requestUrlQuery,
                                    Interaction = Common.StoreInteractionCodes.SystemBundle,
                                };

                                return true;
                            }

                        case 1:
                            {
                                if (hasValidResourceType)
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Interaction = Common.StoreInteractionCodes.TypeCreate,
                                    };

                                    return true;
                                }

                                if (pathComponents[0].Equals("_search", StringComparison.Ordinal))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        Interaction = Common.StoreInteractionCodes.SystemSearch,
                                    };

                                    return true;
                                }

                                if (pathComponents[0].StartsWith('$'))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        OperationName = pathComponents[0],
                                        Interaction = Common.StoreInteractionCodes.SystemOperation,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 2:
                            {
                                if (hasValidResourceType)
                                {
                                    if (pathComponents[1].Equals("_search", StringComparison.Ordinal))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Interaction = Common.StoreInteractionCodes.TypeSearch,
                                        };

                                        return true;
                                    }

                                    if (pathComponents[1].StartsWith('$'))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            OperationName = pathComponents[1],
                                            Interaction = Common.StoreInteractionCodes.TypeOperation,
                                        };

                                        return true;
                                    }
                                }
                            }
                            break;

                        case 3:
                            {
                                if (hasValidResourceType)
                                {
                                    if (pathComponents[2].StartsWith('$'))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Id = pathComponents[1],
                                            OperationName = pathComponents[2],
                                            Interaction = Common.StoreInteractionCodes.InstanceOperation,
                                        };

                                        return true;
                                    }

                                    if (pathComponents[2].Equals("_search", StringComparison.Ordinal))
                                    {
                                        parsed = new()
                                        {
                                            HttpMehtod = _httpMethod,
                                            UrlPath = requestUrlPath,
                                            UrlQuery = requestUrlQuery,
                                            ResourceType = resourceType,
                                            Id = pathComponents[1],
                                            Interaction = Common.StoreInteractionCodes.CompartmentSearch,
                                        };

                                        return true;
                                    }
                                }
                            }
                            break;

                        case 4:
                            {
                                if (hasValidResourceType &&
                                    pathComponents[3].Equals("_search", StringComparison.Ordinal) &&
                                    _store.Keys.Contains(pathComponents[3]))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        CompartmentType = pathComponents[3],
                                        Interaction = Common.StoreInteractionCodes.CompartmentTypeSearch,
                                    };

                                    return true;
                                }
                            }
                            break;
                    }
                }
                break;

            case "DELETE":
                {
                    switch (pathComponents.Length)
                    {
                        case 0:
                            {
                                parsed = new()
                                {
                                    HttpMehtod = _httpMethod,
                                    UrlPath = requestUrlPath,
                                    UrlQuery = requestUrlQuery,
                                    Interaction = Common.StoreInteractionCodes.SystemDeleteConditional,
                                };

                                return true;
                            }

                        case 1:
                            {
                                if (hasValidResourceType)
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Interaction = Common.StoreInteractionCodes.TypeDeleteConditional,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 2:
                            {
                                if (hasValidResourceType &&
                                    (!pathComponents[1].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Interaction = Common.StoreInteractionCodes.InstanceDelete,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 3:
                            {
                                if (hasValidResourceType &&
                                    pathComponents[2].Equals("_history", StringComparison.Ordinal))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Interaction = Common.StoreInteractionCodes.InstanceDeleteHistory,
                                    };

                                    return true;
                                }
                            }
                            break;

                        case 4:
                            {
                                if (hasValidResourceType &&
                                    pathComponents[2].Equals("_history", StringComparison.Ordinal) &&
                                    (!pathComponents[3].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Version = pathComponents[3],
                                        Interaction = Common.StoreInteractionCodes.InstanceDeleteVersion,
                                    };

                                    return true;
                                }
                            }
                            break;
                    }
                }
                break;

            case "PUT":
                {
                    switch (pathComponents.Length)
                    {
                        case 2:
                            {
                                if (hasValidResourceType &&
                                    (!pathComponents[1].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Interaction = Common.StoreInteractionCodes.InstanceUpdate,
                                    };

                                    return true;
                                }
                            }
                            break;
                    }
                }
                break;

            case "PATCH":
                {
                    switch (pathComponents.Length)
                    {
                        case 2:
                            {
                                if (hasValidResourceType &&
                                    (!pathComponents[1].StartsWith('$')))
                                {
                                    parsed = new()
                                    {
                                        HttpMehtod = _httpMethod,
                                        UrlPath = requestUrlPath,
                                        UrlQuery = requestUrlQuery,
                                        ResourceType = resourceType,
                                        Id = pathComponents[1],
                                        Interaction = Common.StoreInteractionCodes.InstancePatch,
                                    };

                                    return true;
                                }
                            }
                            break;
                    }
                }
                break;
        }

        errorMessage = $"TryParseRequest: {_httpMethod} {_url} cannot be parsed into a valid interaction!";
        Console.WriteLine(errorMessage);

        parsed = new()
        {
            ErrorMessage = errorMessage,
            HttpMehtod = _httpMethod,
            UrlPath = requestUrlPath,
            UrlQuery = requestUrlQuery,
        };

        return false;
    }
}
