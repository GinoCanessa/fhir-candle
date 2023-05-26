// <copyright file="INavTracker.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;

namespace fhir.candle.Models;

/// <summary>Interface for navigation tracker.</summary>
public interface INavTracker
{
    /// <summary>Occurs when On Theme Changed.</summary>
    event EventHandler<EventArgs>? OnThemeChanged;

    /// <summary>Gets a value indicating whether this object is dark mode.</summary>
    bool IsDarkMode { get; }

    /// <summary>Notifies a navigation.</summary>
    /// <param name="page"> The page.</param>
    /// <param name="link"> The link.</param>
    /// <param name="depth">The depth.</param>
    void NotifyNav(string page, string link, int depth);
}
