// <copyright file="ZulipClientPool.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using zulip_cs_lib;

namespace fhir.candle.Zulip;

/// <summary>A zulip client pool.</summary>
public class ZulipClientPool
{
    /// <summary>The instance.</summary>
    private static ZulipClientPool _instance;

    /// <summary>The clients.</summary>
    private Dictionary<string, ZulipClient> _clients;

    /// <summary>
    /// Initializes static members of the <see cref="ZulipClientPool"/> class.
    /// </summary>
    static ZulipClientPool()
    {
        _instance = new ZulipClientPool();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZulipClientPool"/> class.
    /// </summary>
    public ZulipClientPool()
    {
        _clients = new Dictionary<string, ZulipClient>();
    }

    /// <summary>Gets the current.</summary>
    public static ZulipClientPool Current => _instance;

    /// <summary>Client key for information.</summary>
    /// <param name="site"> The site.</param>
    /// <param name="email">The email.</param>
    /// <param name="key">  The key.</param>
    /// <returns>A string.</returns>
    public static string ClientKeyForInfo(string site, string email, string key)
    {
        return site + email + key;
    }

    /// <summary>Adds or registers a client.</summary>
    /// <param name="site"> The site.</param>
    /// <param name="email">The email.</param>
    /// <param name="key">  The key.</param>
    /// <returns>The client key.</returns>
    public static string AddOrRegisterClient(
        string site,
        string email,
        string key)
    {
        string clientKey = ClientKeyForInfo(site, email, key);

        if (!_instance._clients.ContainsKey(clientKey))
        {
            _instance._clients.Add(
                clientKey,
                new ZulipClient(site, email, key));
        }

        return clientKey;
    }

    /// <summary>Gets or create client.</summary>
    /// <param name="site"> The site.</param>
    /// <param name="email">The email.</param>
    /// <param name="key">  The key.</param>
    /// <returns>The or create client.</returns>
    public static ZulipClient GetOrCreateClient(
        string site,
        string email,
        string key)
    {
        string clientKey = ClientKeyForInfo(site, email, key);

        if (!_instance._clients.ContainsKey(clientKey))
        {
            _instance._clients.Add(
                clientKey,
                new ZulipClient(site, email, key));
        }

        return _instance._clients[clientKey];
    }
}
