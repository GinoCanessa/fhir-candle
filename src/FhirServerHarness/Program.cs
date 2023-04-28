// <copyright file="Program.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Services;
using FhirStore.Common.Models;

namespace FhirServerHarness;

/// <summary>A program.</summary>
public static class Program
{
    /// <summary>Main entry-point for this application.</summary>
    /// <param name="args">An array of command-line argument strings.</param>
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCors();
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();
        //builder.Services.AddSingleton<IFhirStoreManager, FhirStoreManager>();
        //builder.Services.AddSingleton<IFhirStoreManager>(new FhirStoreManager(BuildTeantConfigurations()));
        builder.Services.AddHostedService<IFhirStoreManager>(sp => new FhirStoreManager(BuildTeantConfigurations()));
        //builder.Services.AddHostedService<FhirStoreManager>();
        //builder.Services.AddSingleton<FhirStoreR4>();

        WebApplication app = builder.Build();

        // we want to essentially disable CORS
        app.UseCors(builder => builder
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());

        app.UseStaticFiles();

        app.UseRouting();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        // warm up services that take a long time to start
        //app.Services.GetService<IFhirStoreManager>()?.Init();
        //await app.Services.GetRequiredService<IFhirStoreManager>().StartAsync();
        //app.Services.GetService<FhirStoreR4>()?.Init();

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
            //    FhirVersion = ProviderConfiguration.FhirVersionCodes.R4,
            //    TenantRoute = "r4",
            //},
            new ProviderConfiguration
            {
                FhirVersion = ProviderConfiguration.SupportedFhirVersions.R4B,
                TenantRoute = "r4b",
                BaseUrl = "http://localhost:5101/r4b",
            },
            //new ProviderConfiguration
            //{
            //    FhirVersion = ProviderConfiguration.FhirVersionCodes.R5,
            //    TenantRoute = "r5",
            //},
        };
    }
}
