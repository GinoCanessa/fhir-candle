// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using fhir.candle.Models;
using fhir.candle.Services;
using FhirCandle.Extensions;
using FhirCandle.Models;
using FhirCandle.Storage;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Hosting.StaticWebAssets;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MudBlazor.Services;

namespace fhir.candle;

/// <summary>A program.</summary>
public static partial class Program
{
    [GeneratedRegex("(http[s]*:\\/\\/.*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    /// <summary>(Immutable) The default listen port.</summary>
    private const int _defaultListenPort = 5826;

    ///// <summary>Candle server delegate.</summary>
    ///// <param name="config">The configuration.</param>
    ///// <returns>An int.</returns>
    //public delegate int CandleServerDelegate(ServerConfiguration config);

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static async Task<int> Main(string[] args)
    {
        // setup our configuration (command line > environment > appsettings.json)
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        Option<string> optPublicUrl = new(
            aliases: new[] { "--url", "-u" },
            getDefaultValue: () => configuration.GetValue("Public_Url", string.Empty) ?? string.Empty,
            "Public URL for the server");

        Option<int?> optListenPort = new(
            aliases: new[] { "--port", "-p" },
            getDefaultValue: () => configuration.GetValue<int?>("Listen_Port", _defaultListenPort) ?? _defaultListenPort,
            "Listen port for the server");

        Option<bool?> optOpenBrowser = new(
            aliases: new[] { "--open-browser", "-o" },
            getDefaultValue: () => configuration.GetValue<bool>("Open_Browser", false),
            "Open a browser once the server starts.");

        Option<int?> optMaxResourceCount = new(
            aliases: new[] { "--max-resources", "-m" },
            getDefaultValue: () => configuration.GetValue<int?>("Max_Resources", null),
            "Maximum number of resources allowed per tenant.");

        Option<bool?> optDisableUi = new(
            name: "--disable-ui",
            getDefaultValue: () => configuration.GetValue<bool?>("Disable_Ui", null),
            "If the server should run headless.");

        Option<string?> optPackageCache = new(
            name: "--fhir-package-cache",
            getDefaultValue: () => configuration.GetValue<string?>("Fhir_Cache", null),
            "Location of the FHIR package cache, for use with registries and IG packages.  Use empty quoted string to disable cache.");

        Option<List<string>> optPublishedPackages = new(
            name: "--load-package",
            getDefaultValue: () => configuration.GetValue<List<string>>("Load_Packages", new List<string>())!,
            "Published packages to load. Specifying package name alone loads highest version.");

        Option<List<string>> optCiPackages = new(
            name: "--ci-package",
            getDefaultValue: () => configuration.GetValue<List<string>>("Ci_Packages", new List<string>())!,
            "Continuous Integration (CI) packages to load. You may specify either just the branch name or a full URL.");

        Option<bool?> optLoadPackageExamples = new(
            name: "--load-examples",
            getDefaultValue: () => configuration.GetValue<bool?>("Load_Examples", null),
            "If package loading should include example instances.");

        Option<string?> optPackageReferenceImplementation = new(
            name: "--reference-implementation",
            getDefaultValue: () => configuration.GetValue<string?>("Reference_Implementation", null),
            "If running as the Reference Implementation, the package directive or literal.");

        Option<string?> optSourceDirectory = new(
            name: "--fhir-source",
            getDefaultValue: () => null,
            "FHIR Contents to load, either in this directory or by subdirectories named per tenant.");

        Option<bool?> optProtectLoadedContent = new(
            name: "--protect-source",
            getDefaultValue: () => null,
            "If any loaded FHIR contents cannot be altered.");

        Option<List<string>> optTenantsR4 = new(
            name: "--r4",
            getDefaultValue: () => new(),
            "FHIR R4 Tenants to provide");

        Option<List<string>> optTenantsR4B = new(
            name: "--r4b",
            getDefaultValue: () => new(),
            "FHIR R4B Tenants to provide");

        Option<List<string>> optTenantsR5 = new(
            name: "--r5",
            getDefaultValue: () => new(),
            "FHIR R5 Tenants to provide");

        Option<List<string>> optTenantsSmartRequired = new(
            name: "--smart-required",
            getDefaultValue: () => new(),
            "FHIR Tenants that require SMART auth");

        Option<List<string>> optTenantsSmartOptional = new(
            name: "--smart-optional",
            getDefaultValue: () => new(),
            "FHIR Tenants that allow (but do not require) SMART auth");

        Option<bool?> optCreateExistingId = new(
            name: "--create-existing-id",
            getDefaultValue: () => configuration.GetValue<bool>("Create_Existing_Id", true),
            "Allow Create interactions (POST) to specify an ID.");

        Option<bool?> optCreateAsUpdate = new(
            name: "--create-as-update",
            getDefaultValue: () => configuration.GetValue<bool>("Create_As_Update", true),
            "Allow Update interactions (PUT) to create new resources.");

        Option<string> optZulipEmail = new(
            name: "--zulip-email",
            getDefaultValue: () => configuration.GetValue("Zulip_Email", string.Empty) ?? string.Empty,
            "Zulip bot email address");

        Option<string> optZulipKey = new(
            name: "--zulip-key",
            getDefaultValue: () => configuration.GetValue("Zulip_Key", string.Empty) ?? string.Empty,
            "Zulip bot API key");

        Option<string> optZulipUrl = new(
            name: "--zulip-url",
            getDefaultValue: () => configuration.GetValue("Zulip_Url", string.Empty) ?? string.Empty,
            "Zulip bot email address");

        Option<string> optSmtpHost = new(
            name: "--smtp-host",
            getDefaultValue: () => configuration.GetValue("SMTP_Host", string.Empty) ?? string.Empty,
            "SMTP Host name/address");

        Option<int?> optSmtpPort = new(
            name: "--smtp-port",
            getDefaultValue: () => configuration.GetValue<int?>("SMTP_Port", null),
            "SMTP Port");

        Option<string> optSmtpUser = new(
            name: "--smtp-user",
            getDefaultValue: () => configuration.GetValue("SMTP_User", string.Empty) ?? string.Empty,
            "SMTP Username");

        Option<string> optSmtpPassword = new(
            name: "--smtp-password",
            getDefaultValue: () => configuration.GetValue("SMTP_Password", string.Empty) ?? string.Empty,
            "SMTP Password");

        Option<string> optFhirPathLabUrl = new(
            name: "--fhirpath-lab-url",
            getDefaultValue: () => configuration.GetValue("FHIRPath_Lab_Url", string.Empty) ?? string.Empty,
            "FHIRPath Lab URL");

        RootCommand rootCommand = new()
        {
            optPublicUrl,
            optListenPort,
            optOpenBrowser,
            optMaxResourceCount,
            optDisableUi,
            optPackageCache,
            optPublishedPackages,
            optCiPackages,
            optLoadPackageExamples,
            optPackageReferenceImplementation,
            optSourceDirectory,
            optProtectLoadedContent,
            optTenantsR4,
            optTenantsR4B,
            optTenantsR5,
            optTenantsSmartRequired,
            optTenantsSmartOptional,
            optCreateExistingId,
            optCreateAsUpdate,
            optZulipEmail,
            optZulipKey,
            optZulipUrl,
            optSmtpHost,
            optSmtpPort,
            optSmtpUser,
            optSmtpPassword,
            optFhirPathLabUrl,
        };

        rootCommand.Description = "A lightweight in-memory FHIR server, for when a small FHIR will do.";

        rootCommand.SetHandler(async (context) =>
        {
            ServerConfiguration config = new()
            {
                PublicUrl = context.ParseResult.GetValueForOption(optPublicUrl) ?? string.Empty,
                ListenPort = context.ParseResult.GetValueForOption(optListenPort) ?? _defaultListenPort,
                OpenBrowser = context.ParseResult.GetValueForOption(optOpenBrowser) ?? false,
                MaxResourceCount = context.ParseResult.GetValueForOption(optMaxResourceCount) ?? 0,
                DisableUi = context.ParseResult.GetValueForOption(optDisableUi) ?? false,
                FhirCacheDirectory = context.ParseResult.GetValueForOption(optPackageCache),
                PublishedPackages = context.ParseResult.GetValueForOption(optPublishedPackages) ?? new(),
                CiPackages = context.ParseResult.GetValueForOption(optCiPackages) ?? new(),
                LoadPackageExamples = context.ParseResult.GetValueForOption(optLoadPackageExamples) ?? false,
                ReferenceImplementation = context.ParseResult.GetValueForOption(optPackageReferenceImplementation) ?? string.Empty,
                SourceDirectory = context.ParseResult.GetValueForOption(optSourceDirectory),
                ProtectLoadedContent = context.ParseResult.GetValueForOption(optProtectLoadedContent) ?? false,
                TenantsR4 = context.ParseResult.GetValueForOption(optTenantsR4) ?? new(),
                TenantsR4B = context.ParseResult.GetValueForOption(optTenantsR4B) ?? new(),
                TenantsR5 = context.ParseResult.GetValueForOption(optTenantsR5) ?? new(),
                SmartRequiredTenants = context.ParseResult.GetValueForOption(optTenantsSmartRequired) ?? new(),
                SmartOptionalTenants = context.ParseResult.GetValueForOption(optTenantsSmartOptional) ?? new(),
                AllowExistingId = context.ParseResult.GetValueForOption(optCreateExistingId) ?? true,
                AllowCreateAsUpdate = context.ParseResult.GetValueForOption(optCreateAsUpdate) ?? true,
                ZulipEmail = context.ParseResult.GetValueForOption(optZulipEmail) ?? string.Empty,
                ZulipKey = context.ParseResult.GetValueForOption(optZulipKey) ?? string.Empty,
                ZulipUrl = context.ParseResult.GetValueForOption(optZulipUrl) ?? string.Empty,
                SmtpHost = context.ParseResult.GetValueForOption(optSmtpHost) ?? string.Empty,
                SmtpPort = context.ParseResult.GetValueForOption(optSmtpPort) ?? 0,
                SmtpUser = context.ParseResult.GetValueForOption(optSmtpUser) ?? string.Empty,
                SmtpPassword = context.ParseResult.GetValueForOption(optSmtpPassword) ?? string.Empty,
                FhirPathLabUrl = context.ParseResult.GetValueForOption(optFhirPathLabUrl) ?? string.Empty,
            };

            await RunServer(config, context.GetCancellationToken());
        });

        //System.CommandLine.Parsing.Parser clParser = new System.CommandLine.Builder.CommandLineBuilder(_rootCommand).Build();

        return await rootCommand.InvokeAsync(args);
    }

    /// <summary>Executes the server operation.</summary>
    /// <param name="config">           The configuration.</param>
    /// <param name="cancellationToken">A token that allows processing to be cancelled.</param>
    /// <returns>An asynchronous result that yields an int.</returns>
    public static async Task<int> RunServer(ServerConfiguration config, CancellationToken cancellationToken)
    { 
        try
        {
            if (string.IsNullOrEmpty(config.PublicUrl))
            {
                config.PublicUrl = $"http://localhost:{config.ListenPort}";
            }

            // update configuration to make sure listen url is properly formatted
            Match match = InputUrlFormatRegex().Match(config.PublicUrl);
            config.PublicUrl = match.ToString();

            if (config.PublicUrl.EndsWith('/'))
            {
                config.PublicUrl = config.PublicUrl.Substring(0, config.PublicUrl.Length - 1);
            }

            if (config.FhirPathLabUrl.EndsWith('/'))
            {
                config.FhirPathLabUrl = config.FhirPathLabUrl.Substring(0, config.FhirPathLabUrl.Length - 1);
            }

            // check for no tenants (create defaults)
            if ((!config.TenantsR4.Any()) &&
                (!config.TenantsR4B.Any()) &&
                (!config.TenantsR5.Any()))
            {
                config.TenantsR4.Add("r4");
                config.TenantsR4B.Add("r4b");
                config.TenantsR5.Add("r5");
            }

            if (config.FhirCacheDirectory == null)
            {
                config.FhirCacheDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".fhir");
            }

            Dictionary<string, TenantConfiguration> tenants = BuildTenantConfigurations(config);

            WebApplicationBuilder builder = null!;

            // when packaging as a dotnet tool, we need to do some directory shenanigans for the static content root
            string root = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location ?? AppContext.BaseDirectory) ?? string.Empty;
            if (!string.IsNullOrEmpty(root))
            {
                string webRoot = FindRelativeDir(root, "staticwebassets", false);

                if ((!string.IsNullOrEmpty(webRoot)) && Directory.Exists(webRoot))
                {
                    builder = WebApplication.CreateBuilder(new WebApplicationOptions()
                    {
                        WebRootPath = webRoot,
                    });
                }
            }

            if (builder == null)
            {
                builder = WebApplication.CreateBuilder();
            }

            StaticWebAssetsLoader.UseStaticWebAssets(builder.Environment, builder.Configuration);

            //builder.WebHost.UseWebRoot("wwwroot");

            builder.WebHost.UseStaticWebAssets();

            builder.Services.AddCors();

            // add our configuration
            builder.Services.AddSingleton(config);
            builder.Services.AddSingleton(tenants);

            // add a FHIR-Store singleton, then register as a hosted service
            builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
            builder.Services.AddHostedService<IFhirStoreManager>(sp => sp.GetRequiredService<IFhirStoreManager>());

            // add a notification manager singleton, then register as a hosted service
            builder.Services.AddSingleton<INotificationManager, NotificationManager>();
            builder.Services.AddHostedService<INotificationManager>(sp => sp.GetRequiredService<INotificationManager>());

            // add a package service singleton, then register as a hosted service
            builder.Services.AddSingleton<IFhirPackageService, FhirPackageService>();
            builder.Services.AddHostedService<IFhirPackageService>(sp => sp.GetRequiredService<IFhirPackageService>());

            // add a SMART Authorization singleton, then register as a hosted service
            builder.Services.AddSingleton<ISmartAuthManager, SmartAuthManager>();
            builder.Services.AddHostedService<ISmartAuthManager>(sp => sp.GetRequiredService<ISmartAuthManager>());

            builder.Services.AddControllers();

            if (config.DisableUi == true)
            {
                // check for any SMART-enabled tenants - *requires* UI
                if (config.SmartRequiredTenants.Any() || config.SmartOptionalTenants.Any())
                {
                    Console.WriteLine("fhir-candle <<< ERROR: Cannot disable UI when SMART is configured.");
                    return -1;
                }
            }
            else
            {
                builder.Services.AddRazorPages(options =>
                {
                    options.Conventions.AddPageRoute("/store", "/store/{storeName}");
                });
                builder.Services.AddServerSideBlazor();
                builder.Services.AddMudServices();

                // set our default UI page
                //Pages.Index.Mode = config.UiMode;
            }

            string localUrl = $"http://*:{config.ListenPort}";

            builder.WebHost.UseUrls(localUrl);
            //builder.WebHost.UseStaticWebAssets();

            WebApplication app = builder.Build();

            // we want to essentially disable CORS
            app.UseCors(builder => builder
                .AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders(new[] { "Content-Location", "Location", "Etag", "Last-Modified" }));

            app.UseStaticFiles();

            app.UseRouting();

            app.MapControllers();

            // this is developer tooling - always respond with as much detail as we can
            app.UseDeveloperExceptionPage();

            if (config.DisableUi != true)
            {
                app.MapBlazorHub();
                app.MapFallbackToPage("/_Host");
            }

            IFhirPackageService ps = app.Services.GetRequiredService<IFhirPackageService>();
            IFhirStoreManager sm = app.Services.GetRequiredService<IFhirStoreManager>();
            ISmartAuthManager am = app.Services.GetRequiredService<ISmartAuthManager>();

            // perform slow initialization of services
            ps.Init();          // store manager requires Package Service to be initialized
            sm.Init();          // store manager may need to download packages
            am.Init();          // spin up authorization manager

            // run the server
            //await app.RunAsync(cancellationToken);
            _ = app.StartAsync();

            AfterServerStart(app, config);
            await app.WaitForShutdownAsync(cancellationToken);

            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"fhir-candle <<< caught exception: {ex.Message}");
            return -1;
        }
    }


    /// <summary>After server start.</summary>
    /// <param name="app">   The application.</param>
    /// <param name="config">The configuration.</param>
    private static void AfterServerStart(WebApplication app, ServerConfiguration config)
    {
        Console.WriteLine("Press CTRL+C to exit");

        if (config.OpenBrowser == true)
        {
            string url = $"http://localhost:{config.ListenPort}";

            LaunchBrowser(url);
        }
    }

    private static void LaunchBrowser(string url)
    {
        ProcessStartInfo psi = new();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            psi.FileName = "open";
            psi.ArgumentList.Add(url);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            psi.FileName = "xdg-open";
            psi.ArgumentList.Add(url);
        }
        else
        {
            psi.FileName = "cmd";
            psi.ArgumentList.Add("/C");
            psi.ArgumentList.Add("start");
            psi.ArgumentList.Add(url);
        }

        Process.Start(psi);
    }

    /// <summary>Builds an enumeration of tenant configurations for this application.</summary>
    /// <param name="config">The configuration.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process build tenant configurations in this
    /// collection.
    /// </returns>
    private static Dictionary<string, TenantConfiguration> BuildTenantConfigurations(ServerConfiguration config)
    {
        HashSet<string> smartRequired = config.SmartRequiredTenants.ToHashSet();
        HashSet<string> smartOptional = config.SmartOptionalTenants.ToHashSet();

        Dictionary<string, TenantConfiguration> tenants = new();

        foreach (string tenant in config.TenantsR4)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = TenantConfiguration.SupportedFhirVersions.R4,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
                SmartRequired = smartRequired.Contains(tenant),
                SmartAllowed = smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
            });
        }

        foreach (string tenant in config.TenantsR4B)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = TenantConfiguration.SupportedFhirVersions.R4B,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
                SmartRequired = smartRequired.Contains(tenant),
                SmartAllowed = smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
            });
        }

        foreach (string tenant in config.TenantsR5)
        {
            tenants.Add(tenant, new()
            {
                FhirVersion = TenantConfiguration.SupportedFhirVersions.R5,
                ControllerName = tenant,
                BaseUrl = config.PublicUrl + "/fhir/" + tenant,
                ProtectLoadedContent = config.ProtectLoadedContent,
                MaxResourceCount = config.MaxResourceCount,
                SmartRequired = smartRequired.Contains(tenant),
                SmartAllowed = smartOptional.Contains(tenant),
                AllowExistingId = config.AllowExistingId,
                AllowCreateAsUpdate = config.AllowCreateAsUpdate,
            });
        }

        DirectoryInfo? loadDir = null;

        if (!string.IsNullOrEmpty(config.SourceDirectory))
        {
            if (Path.IsPathRooted(config.SourceDirectory) &&
                Directory.Exists(config.SourceDirectory))
            {
                loadDir = new DirectoryInfo(config.SourceDirectory);
            }
            else
            {
                // look for a relative directory, starting in the running directory
                string relativeDir = FindRelativeDir(string.Empty, config.SourceDirectory, false);

                if (!string.IsNullOrEmpty(relativeDir))
                {
                    loadDir = new DirectoryInfo(relativeDir);
                }
            }
        }

        if (loadDir != null)
        {
            foreach (TenantConfiguration tenant in tenants.Values)
            {
                // check for a tenant-named sub-directory
                string subPath = Path.Combine(loadDir.FullName, tenant.ControllerName);
                if (Directory.Exists(subPath))
                {
                    tenant.LoadDirectory = new DirectoryInfo(subPath);
                }
                else
                {
                    tenant.LoadDirectory = loadDir;
                }
            }
        }

        return tenants;
    }

    /// <summary>Searches for the FHIR specification directory.</summary>
    /// <exception cref="DirectoryNotFoundException">Thrown when the requested directory is not
    ///  present.</exception>
    /// <param name="dirName">       The name of the directory we are searching for.</param>
    /// <param name="throwIfNotFound">(Optional) True to throw if not found.</param>
    /// <returns>The found FHIR directory.</returns>
    public static string FindRelativeDir(
        string startDir,
        string dirName,
        bool throwIfNotFound = true)
    {
        string currentDir = string.IsNullOrEmpty(startDir) ? Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty : startDir;
        string testDir = Path.Combine(currentDir, dirName);

        while (!Directory.Exists(testDir))
        {
            currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));

            if (currentDir == Path.GetPathRoot(currentDir))
            {
                if (throwIfNotFound)
                {
                    throw new DirectoryNotFoundException($"Could not find directory {dirName}!");
                }

                return string.Empty;
            }

            testDir = Path.Combine(currentDir, dirName);
        }

        return testDir;
    }
}
