// <copyright file="ReceivedSubscriptionChangedEventArgs.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace FhirStore.Models;

/// <summary>Additional information for subscription changed events.</summary>
public class ReceivedSubscriptionChangedEventArgs
{
    /// <summary>Gets or initializes the tenant.</summary>
    public required TenantConfiguration Tenant { get; init; }

    /// <summary>Gets or initializes the identifier of the subscription.</summary>
    public required string SubscriptionReference { get; init; }

    /// <summary>Gets or initializes the number of current bundles.</summary>
    public required int CurrentBundleCount { get; init; }

    /// <summary>Gets or initializes a value indicating whether the removed.</summary>
    public required bool Removed { get; init; }
}
