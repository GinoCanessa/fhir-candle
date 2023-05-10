// <copyright file="TopicConverter.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Models;

namespace FhirStore.Versioned.Shims.Subscriptions;

/// <summary>A topic format converter.</summary>
public class TopicConverter
{
    /// <summary>
    /// Attempts to parse a FHIR SubscriptionTopic.
    /// </summary>
    /// <param name="topic"> The topic.</param>
    /// <param name="common">[out] The common.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryParse(object topic, out ParsedSubscriptionTopic common)
    {
        throw new NotImplementedException();
    }
}