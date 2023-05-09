// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Text.RegularExpressions;
using FhirServerHarness.Services;
using FhirStore.Common.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using MudBlazor.Services;

namespace FhirServerHarness;

/// <summary>A program.</summary>
public static partial class Program
{
    [GeneratedRegex("(http[s]*:\\/\\/[A-Za-z0-9\\.]*(:\\d+)*)")]
    private static partial Regex InputUrlFormatRegex();

    private static IConfiguration _configuration = null!;

    /// <summary>Gets or sets the configuration.</summary>
    /// <value>The configuration.</value>
    public static IConfiguration Configuration => _configuration;

    /// <summary>Gets or sets URL of the public.</summary>
    /// <value>The public URL.</value>
    public static string PublicUrl { get; set; } = string.Empty;

    /// <summary>Gets or sets URL of the public.</summary>
    /// <value>The internal URL.</value>
    public static string InternalUrl { get; set; } = string.Empty;

    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static void Main(string[] args)
    {
        // setup our configuration (command line > environment > appsettings.json)
        _configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // update configuration to make sure listen url is properly formatted
        Regex regex = InputUrlFormatRegex();
        Match match = regex.Match(Configuration["Server_Public_Url"] ?? string.Empty);
        Configuration["Server_Public_Url"] = match.ToString();
        PublicUrl = match.ToString();

        match = regex.Match(Configuration["Server_Internal_Url"] ?? string.Empty);
        Configuration["Server_Internal_Url"] = match.ToString();
        InternalUrl = match.ToString();


        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        IEnumerable<ProviderConfiguration> configurations = BuildTeantConfigurations();

        builder.Services.AddCors();
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        //builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
        builder.Services.AddSingleton<IFhirStoreManager>(new FhirStoreManager(configurations));
        //builder.Services.AddHostedService<IFhirStoreManager>(sp => new FhirStoreManager(configurations));

        builder.Services.AddMudServices();

        WebApplication app = builder.Build();

        // we want to essentially disable CORS
        app.UseCors(builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());

        app.UseStaticFiles();

        app.UseRouting();

        app.MapControllers();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        // warm up services that take a long time to start
        //app.Services.GetService<IFhirStoreManager>()?.Init();
        //await app.Services.GetRequiredService<IFhirStoreManager>().StartAsync();
        //app.Services.GetService<FhirStoreR4>()?.Init();

        // run the server
        app.Run();
    }

    /// <summary>Builds an enumeration of teant configurations for this application.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process build teant configurations in this
    /// collection.
    /// </returns>
    private static IEnumerable<ProviderConfiguration> BuildTeantConfigurations()
    {
        return new List<ProviderConfiguration>
        {
            //new ProviderConfiguration
            //{
            //    FhirVersion = ProviderConfiguration.SupportedFhirVersions.R4,
            //    ControllerName = "r4",
            //    BaseUrl = PublicUrl + "/fhir/r4",
            //},
            //new ProviderConfiguration
            //{
            //    FhirVersion = ProviderConfiguration.SupportedFhirVersions.R4B,
            //    ControllerName = "r4b",
            //    BaseUrl = PublicUrl + "/fhir/r4b",
            //},
            new ProviderConfiguration
            {
                FhirVersion = ProviderConfiguration.SupportedFhirVersions.R5,
                ControllerName = "r5",      // route will be /fhir/r5
                BaseUrl = PublicUrl + "/fhir/r5",
            },
        };
    }

}
