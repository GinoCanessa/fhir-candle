// <copyright file="NotificationManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Zulip;
using FhirStore.Models;
using FhirStore.Storage;
using System.Text;

namespace FhirServerHarness.Services;

/// <summary>Manager for notifications.</summary>
public class NotificationManager : INotificationManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The HTTP client for REST notifications.</summary>
    private HttpClient _httpClient = new();

    /// <summary>The logger.</summary>
    private ILogger _logger;

    /// <summary>Manager for store.</summary>
    private IFhirStoreManager _storeManager;

    /// <summary>The heartbeat timer.</summary>
    private Timer _heartbeatTimer = null!;

    /// <summary>The zulip site URL (e.g., https://chat.fhir.org/).</summary>
    private string _zulipSite;

    /// <summary>The zulip bot email address.</summary>
    private string _zulipEmail;

    /// <summary>The zulip bot API key.</summary>
    private string _zulipKey;

    /// <summary>Initializes a new instance of the <see cref="NotificationManager"/> class.</summary>
    /// <param name="fhirStoreManager">Manager for FHIR store.</param>
    public NotificationManager(ILogger<NotificationManager> logger, IFhirStoreManager fhirStoreManager)
    {
        _logger = logger;
        _storeManager = fhirStoreManager;

        _zulipSite = Program.Configuration["Zulip_Site"] ?? string.Empty;
        _zulipEmail = Program.Configuration["Zulip_Email"] ?? string.Empty;
        _zulipKey = Program.Configuration["Zulip_Key"] ?? string.Empty;

        if (string.IsNullOrEmpty(_zulipSite) ||
            string.IsNullOrEmpty(_zulipEmail) ||
            string.IsNullOrEmpty(_zulipKey))
        {
            _logger.LogInformation("Zulip information not found - Zulip notification will not be sent");
        }
        else
        {
            _logger.LogInformation($"Found zulip configuration for: {_zulipSite} ({_zulipEmail})");
            ZulipClientPool.AddOrRegisterClient(_zulipSite, _zulipEmail, _zulipKey);
        }
    }

    /// <summary>Try notify via the appropriate channel type.</summary>
    /// <param name="store">The store.</param>
    /// <param name="e">    Subscription event information.</param>
    /// <returns>An asynchronous result that yields true if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotify(IFhirStore store, SubscriptionEventArgs e)
    {
        string contents;

        switch (e.NotificationType)
        {
            case ParsedSubscription.NotificationTypeCodes.Handshake:
                {
                    contents = store.SerializeSubscriptionEvents(
                                    e.Subscription.Id,
                                    Array.Empty<long>(),
                                    "handshake");
                }
                break;

            case ParsedSubscription.NotificationTypeCodes.Heartbeat:
                {
                    contents = store.SerializeSubscriptionEvents(
                                    e.Subscription.Id,
                                    Array.Empty<long>(),
                                    "heartbeat");
                }
                break;

            case ParsedSubscription.NotificationTypeCodes.EventNotification:
                {
                    if (!e.NotificationEvents.Any())
                    {
                        return false;
                    }

                    contents = store.SerializeSubscriptionEvents(
                                    e.Subscription.Id,
                                    e.NotificationEvents.Select(ne => ne.EventNumber),
                                    "event-notification");
                }
                break;

            case ParsedSubscription.NotificationTypeCodes.QueryStatus:
                throw new NotImplementedException("TryNotify <<< QueryStatus is not an implemented mode for notifications");
            //break;

            case ParsedSubscription.NotificationTypeCodes.QueryEvent:
                throw new NotImplementedException("TryNotify <<< QueryEvent is not an implemented mode for notifications");
            //break;

            default:
                _logger.LogError($"TryNotify <<< Unknown notification type: {e.NotificationType}");
                return false;
        }

        switch (e.Subscription.ChannelCode.ToLowerInvariant())
        {
            case "email":
            case "websocket":
                throw new NotImplementedException($"TryNotify <<< unimplemented channel type: {e.Subscription.ChannelCode}");

            case "zulip":
                return await TryNotifyZulip(store, e, contents);

            case "rest-hook":
                return await TryNotifyRestHook(store, e, contents);

            default:
                return false;
        }
    }

    /// <summary>Attempt to send a notification via REST Hook.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotifyRestHook(
        IFhirStore store,
        SubscriptionEventArgs e,
        string contents)
    {
        // auto-pass any notifications to example.org
        if (e.Subscription.Endpoint.Contains("example.org", StringComparison.Ordinal))
        {
            return true;
        }

        HttpRequestMessage request = null!;

        // send the request to the endpoint
        try
        {
            // build our request
            request = new HttpRequestMessage()
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(e.Subscription.Endpoint),
                Content = new StringContent(contents, Encoding.UTF8, e.Subscription.ContentType),
            };

            // check for additional headers
            if ((e.Subscription.Parameters != null) && e.Subscription.Parameters.Any())
            {
                // add headers
                foreach ((string param, List<string> values) in e.Subscription.Parameters)
                {
                    if (string.IsNullOrEmpty(param) ||
                        (!values.Any()))
                    {
                        continue;
                    }

                    request.Headers.Add(param, values);
                }
            }

            // send our request
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            // check the status code
            if ((response.StatusCode != System.Net.HttpStatusCode.OK) &&
                (response.StatusCode != System.Net.HttpStatusCode.Accepted) &&
                (response.StatusCode != System.Net.HttpStatusCode.NoContent))
            {
                // failure
                e.Subscription.RegisterError($"POST to {e.Subscription.Endpoint} failed: {response.StatusCode}");
                return false;
            }

            e.Subscription.LastCommunicationTicks = DateTime.Now.Ticks;

            if (e.NotificationEvents.Any())
            {
                _logger.LogInformation(
                    $" <<< Subscription/{e.Subscription.Id}" +
                    $" POST: {e.Subscription.Endpoint}" +
                    $" Events: {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}");
            }
            else
            {
                _logger.LogInformation(
                    $" <<< Subscription/{e.Subscription.Id}" +
                    $" POST {e.NotificationType}: {e.Subscription.Endpoint}");
            }
        }
        catch (Exception ex)
        {
            e.Subscription.RegisterError($"POST {e.NotificationType} to {e.Subscription.Endpoint} failed: {ex.Message}");
            return false;
        }
        finally
        {
            if (request != null)
            {
                request.Dispose();
            }
        }

        return true;
    }

    /// <summary>Attempt to send a notification via Zulip.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>An asynchronous result that yields true if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotifyZulip(
        IFhirStore store,
        SubscriptionEventArgs e,
        string contents)
    {
        string zulipSite = e.Subscription.Parameters.ContainsKey("site") ? e.Subscription.Parameters["site"].First() : _zulipSite;
        string zulipEmail = e.Subscription.Parameters.ContainsKey("email") ? e.Subscription.Parameters["email"].First() : _zulipEmail;
        string zulipKey = e.Subscription.Parameters.ContainsKey("key") ? e.Subscription.Parameters["key"].First() : _zulipKey;

        if (string.IsNullOrEmpty(zulipSite) ||
            string.IsNullOrEmpty(zulipEmail) ||
            string.IsNullOrEmpty(zulipKey))
        {
            return false;
        }

        // send the request to the endpoint
        try
        {
            zulip_cs_lib.ZulipClient client = ZulipClientPool.GetOrCreateClient(zulipSite, zulipEmail, zulipKey);

            if (client == null)
            {
                return false;
            }

            string messageText = BuildZulipMessage(store, e, contents);

            if (e.Subscription.Parameters.ContainsKey("streamId"))
            {
                foreach (string value in e.Subscription.Parameters["streamId"])
                {
                    if (!int.TryParse(value, out int id))
                    {
                        continue;
                    }

                    (bool success, string details, ulong messageId) result = await client.Messages.TrySendStream(
                        messageText,
                        "Subscription Notification",
                        new int[] { id });
                }
            }

            if (e.Subscription.Parameters.ContainsKey("userId"))
            {
                foreach (string value in e.Subscription.Parameters["userId"])
                {
                    if (!int.TryParse(value, out int id))
                    {
                        continue;
                    }

                    (bool success, string details, ulong messageId) result = await client.Messages.TrySendPrivate(
                        messageText,
                        new int[] { id });
                }
            }

            e.Subscription.LastCommunicationTicks = DateTime.Now.Ticks;
        }
        catch (Exception ex)
        {
            e.Subscription.RegisterError($"Zulip {e.NotificationType} to {e.Subscription.Endpoint} failed: {ex.Message}");
            return false;
        }

        return true;
    }

    /// <summary>Builds zulip message.</summary>
    /// <param name="store">   The store.</param>
    /// <param name="e">       Subscription event information.</param>
    /// <param name="contents">Serialized contents of the notification.</param>
    /// <returns>A string.</returns>
    private string BuildZulipMessage(IFhirStore store, SubscriptionEventArgs e, string contents)
    {
        // TODO: need to build a persistent opt-out

        string shortMime = e.Subscription.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
            ? "json"
            : "xml";

        switch (e.NotificationType)
        {
            case ParsedSubscription.NotificationTypeCodes.Handshake:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is going to send you notifications.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.Heartbeat:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is alive with nothing to report.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.EventNotification:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is reporting notification" +
                    $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.QueryStatus:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is responding to a status query" +
                    $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n" +
                    $"To opt out, please click ~~here~~" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            case ParsedSubscription.NotificationTypeCodes.QueryEvent:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" is resending events" +
                    $" {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}.\n" +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";

            default:
                return $"[{e.Subscription.Id}]({store.Config.BaseUrl}/Subscription/{e.Subscription.Id})" +
                    $" type: `{e.NotificationType}`." +
                    $"To opt out, please click ~~here~~\n" +
                    $"```spoiler Bundle\n```{shortMime}\n{contents}\n```\n```\n";
        }
    }

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting NotificationManager...");

        // traverse stores and initialize our event handlers
        foreach (IFhirStore store in _storeManager.Values)
        {
            // register our event handlers
            store.OnSubscriptionsChanged += Store_OnSubscriptionsChanged;
            store.OnSubscriptionEvent += Store_OnSubscriptionEvent;
        }

        // start our heartbeat timer
        _heartbeatTimer = new Timer(
                CheckAndSendHeartbeats,
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(2));

        return Task.CompletedTask;
    }

    /// <summary>Check and send heartbeats.</summary>
    /// <param name="state">The state.</param>
    private void CheckAndSendHeartbeats(object? state)
    {
        long currentTicks = DateTime.Now.Ticks;

        // traverse stores to check subscriptions
        foreach (IFhirStore store in _storeManager.Values)
        {
            // traverse active subscriptions
            foreach (ParsedSubscription sub in store.CurrentSubscriptions)
            {
                if ((!sub.CurrentStatus.Equals("active", StringComparison.Ordinal)) ||
                    (sub.HeartbeatSeconds <= 0))
                {
                    continue;
                }

                long threshold = currentTicks - (sub.HeartbeatSeconds * TimeSpan.TicksPerSecond);

                if (sub.LastCommunicationTicks < threshold)
                {
                    sub.LastCommunicationTicks = currentTicks;

                    _ = TryNotify(store, new()
                    {
                        Tenant = store.Config,
                        Subscription = sub,
                        NotificationType = ParsedSubscription.NotificationTypeCodes.Heartbeat,
                    });
                }
            }
        }
    }

    /// <summary>Event handler. Called by Store for on subscription events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription event information.</param>
    private void Store_OnSubscriptionEvent(object? sender, SubscriptionEventArgs e)
    {
        if (!_storeManager.ContainsKey(e.Tenant.ControllerName))
        {
            _logger.LogError($"Cannot send subscription for non-existing tenant: {e.Tenant.ControllerName}");
            return;
        }

        _ = TryNotify(_storeManager[e.Tenant.ControllerName], e);
    }

    /// <summary>Event handler. Called by Store for on subscriptions changed events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription changed event information.</param>
    private void Store_OnSubscriptionsChanged(object? sender, SubscriptionChangedEventArgs e)
    {
        // make sure the store we want exists
        if (!_storeManager.ContainsKey(e.Tenant.ControllerName))
        {
            return;
        }

        // check for a deleted subscription
        if (!string.IsNullOrEmpty(e.RemovedSubscriptionId))
        {
            // TODO: Remove any existing heartbeat record
        }

        IFhirStore store = _storeManager[e.Tenant.ControllerName];

        // check for a new subscription
        if (e.SendHandshake)
        {
            if (e.ChangedSubscription == null)
            {
                return;
            }

            bool success = TryNotify(store, new()
            {
                Tenant = e.Tenant,
                Subscription = e.ChangedSubscription!,
                NotificationType = ParsedSubscription.NotificationTypeCodes.Handshake,
            }).Result;

            if (success)
            {
                e.ChangedSubscription!.CurrentStatus = "active";
            }
            else
            {
                e.ChangedSubscription!.CurrentStatus = "error";
            }
        }

        // check for a changed subscription
        if (e.ChangedSubscription != null)
        {
            // TODO: Check for changes to the heartbeat interval
        }
    }

    /// <summary>Triggered when the application host is performing a graceful shutdown.</summary>
    /// <param name="cancellationToken">Indicates that the shutdown process should no longer be
    ///  graceful.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StopAsync(CancellationToken cancellationToken)
    {
        _heartbeatTimer?.Change(Timeout.Infinite, 0);

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
                _heartbeatTimer?.Dispose();
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
