// <copyright file="NotificationManager.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Models;
using FhirStore.Storage;
using System.Reflection.PortableExecutable;
using System.Text;

namespace FhirServerHarness.Services;

/// <summary>Manager for notifications.</summary>
public class NotificationManager : INotificationManager
{
    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The HTTP client for REST notifications.</summary>
    private HttpClient _httpClient = new();

    /// <summary>Manager for store.</summary>
    private IFhirStoreManager _storeManager;

    /// <summary>Initializes a new instance of the <see cref="NotificationManager"/> class.</summary>
    /// <param name="fhirStoreManager">Manager for FHIR store.</param>
    public NotificationManager(IFhirStoreManager fhirStoreManager)
    {
        _storeManager = fhirStoreManager;
    }

    /// <summary>Attempts to notify REST hook.</summary>
    /// <param name="store">           The store.</param>
    /// <param name="e">            Subscription event information.</param>
    /// <param name="notificationType">(Optional) Type of the notification.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    private async Task<bool> TryNotifyRestHook(
        IFhirStore store,
        SubscriptionEventArgs e)
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
            // serialize our contents
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
                    throw new NotImplementedException();
                    //break;

                case ParsedSubscription.NotificationTypeCodes.QueryEvent:
                    throw new NotImplementedException();
                    //break;
    
                default:
                    Console.WriteLine($"Unknown notification type: {e.NotificationType}");
                    return false;
            }

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

            if (e.NotificationEvents.Any())
            {
                Console.WriteLine(
                    $" <<< Subscription/{e.Subscription.Id}" +
                    $" POST: {e.Subscription.Endpoint}" +
                    $" Events: {string.Join(',', e.NotificationEvents.Select(ne => ne.EventNumber))}");
            }
            else
            {
                Console.WriteLine(
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

    /// <summary>Triggered when the application host is ready to start the service.</summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    /// <returns>An asynchronous result.</returns>
    Task IHostedService.StartAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine("Starting NotificationManager...");

        // traverse stores and initialize our event handlers
        foreach (IFhirStore store in _storeManager.Values)
        {
            // register our event handlers
            store.OnSubscriptionsChanged += Store_OnSubscriptionsChanged;
            store.OnSubscriptionEvent += Store_OnSubscriptionEvent;
        }

        return Task.CompletedTask;
    }

    /// <summary>Event handler. Called by Store for on subscription events.</summary>
    /// <param name="sender">The sender.</param>
    /// <param name="e">     Subscription event information.</param>
    private void Store_OnSubscriptionEvent(object? sender, SubscriptionEventArgs e)
    {
        if (!_storeManager.ContainsKey(e.Tenant.ControllerName))
        {
            Console.WriteLine($"Cannot send subscription for non-existing tenant: {e.Tenant.ControllerName}");
            return;
        }

        switch (e.Subscription.ChannelCode.ToLowerInvariant())
        {
            case "zulip":
            case "email":
            case "websocket":
                break;

            case "rest-hook":
                _ = TryNotifyRestHook(_storeManager[e.Tenant.ControllerName], e);
                break;
        }
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

            bool success = TryNotifyRestHook(store, new()
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
