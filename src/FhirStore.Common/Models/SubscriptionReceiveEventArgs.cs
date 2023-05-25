// <copyright file="SubscriptionReceiveEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirStore.Models;

/// <summary>Additional information for subscription events.</summary>
public class SubscriptionReceiveEventArgs : EventArgs
{
    /// <summary>Gets the tenant.</summary>
    public required TenantConfiguration Tenant { get; init; }

    /// <summary>
    /// Gets the BundleId that contains this notification.
    /// </summary>
    public required string BundleId { get; init; }

    public required ParsedSubscriptionStatus Status { get; init; }
}
