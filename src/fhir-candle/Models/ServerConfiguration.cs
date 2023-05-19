﻿// <copyright file="ServerConfiguration.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace fhir.candle.Models;

/// <summary>A server configuration.</summary>
public class ServerConfiguration
{
    /// <summary>Gets or sets URL of the public.</summary>
    public string PublicUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the listen port.</summary>
    public required int ListenPort { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to not load the GUI.
    /// </summary>
    public bool NoGui { get; set; } = false;

    /// <summary>Gets or sets the pathname of the source directory.</summary>
    public DirectoryInfo? SourceDirectory { get; set; } = null;


    /// <summary>Gets or sets the FHIR R4 tenants.</summary>
    public List<string> TenantsR4 { get; set; } = new();

    /// <summary>Gets or sets the FHIR R4B tenants.</summary>
    public List<string> TenantsR4B { get; set; } = new();

    /// <summary>Gets or sets the FHIR R5 tenants.</summary>
    public List<string> TenantsR5 { get; set; } = new();


    /// <summary>Gets or sets the zulip email.</summary>
    public string ZulipEmail { get; set; } = string.Empty;

    /// <summary>Gets or sets the zulip key.</summary>
    public string ZulipKey { get; set; } = string.Empty;

    /// <summary>Gets or sets URL of the zulip site.</summary>
    public string ZulipUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP host.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP port.</summary>
    public int SmtpPort { get; set; } = 587;

    /// <summary>Gets or sets the SMTP user.</summary>
    public string SmtpUser { get; set; } = string.Empty;

    /// <summary>Gets or sets the SMTP password.</summary>
    public string SmtpPassword { get; set; } = string.Empty;
}