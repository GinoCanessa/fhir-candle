// <copyright file="Utils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirCandle.Models;

namespace fhir.candle.Models;

/// <summary>An utilities.</summary>
public static class Utils
{
    /// <summary>Gets ri pages.</summary>
    /// <param name="currentRi">The current ri.</param>
    /// <param name="pagesR4">  [out] The RI Pages for FHIR R4.</param>
    /// <param name="pagesR4B"> [out] The RI Pages for FHIR R4B.</param>
    /// <param name="pagesR5">  [out] The RI Pages for FHIR R5.</param>
    public static void GetRiPages(
        string currentRi,
        out IEnumerable<RiPageInfo> pagesR4,
        out IEnumerable<RiPageInfo> pagesR4B,
        out IEnumerable<RiPageInfo> pagesR5)
    {
        List<RiPageInfo> r4 = new();
        List<RiPageInfo> r4b = new();
        List<RiPageInfo> r5 = new();

        IEnumerable<Type> riPageTypes = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IRiPage)));
        AddRiPages(currentRi, r4, r4b, r5, riPageTypes);

        riPageTypes = typeof(FhirCandle.Ui.R4.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IRiPage)));
        AddRiPages(currentRi, r4, r4b, r5, riPageTypes);

        riPageTypes = typeof(FhirCandle.Ui.R4B.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IRiPage)));
        AddRiPages(currentRi, r4, r4b, r5, riPageTypes);

        riPageTypes = typeof(FhirCandle.Ui.R5.Subscriptions.TourUtils).Assembly.GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IRiPage)));
        AddRiPages(currentRi, r4, r4b, r5, riPageTypes);

        pagesR4 = r4.AsEnumerable();
        pagesR4B = r4b.AsEnumerable();
        pagesR5 = r5.AsEnumerable();

        static void AddRiPages(string currentRi, List<RiPageInfo> r4, List<RiPageInfo> r4b, List<RiPageInfo> r5, IEnumerable<Type> riPageTypes)
        {
            foreach (Type riPageType in riPageTypes)
            {
                RiPageInfo info = new()
                {
                    ContentForPackage = riPageType.GetProperty("ContentForPackage", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                    PageName = riPageType.GetProperty("PageName", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                    Description = riPageType.GetProperty("Description", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                    RoutePath = riPageType.GetProperty("RoutePath", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                    FhirVersionLiteral = riPageType.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                    FhirVersionNumeric = riPageType.GetProperty("FhirVersionNumeric", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                };

                if (!info.ContentForPackage.Equals(currentRi, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                switch (info.FhirVersionLiteral)
                {
                    case "R4":
                        r4.Add(info);
                        break;

                    case "R4B":
                        r4b.Add(info);
                        break;

                    case "R5":
                        r5.Add(info);
                        break;

                    case "":
                        r4.Add(info);
                        r4b.Add(info);
                        r5.Add(info);
                        break;
                }
            }
        }
    }

    /// <summary>Gets additional index content.</summary>
    /// <param name="currentRi"> The current ri.</param>
    /// <param name="contentR4"> [out] The fourth content r.</param>
    /// <param name="contentR4B">[out] The content r 4 b.</param>
    /// <param name="contentR5"> [out] The fifth content r.</param>
    public static void GetAdditionalIndexContent(
        string currentRi,
        out Type? contentR4,
        out Type? contentR4B,
        out Type? contentR5)
    {
        contentR4 = null;
        contentR4B = null;
        contentR5 = null;

        IEnumerable<Type> contentTypes = System.Reflection.Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t.GetInterfaces().Contains(typeof(IIndexContent)));

        foreach (Type contentType in contentTypes)
        {
            IndexContentInfo info = new()
            {
                ContentForPackage = contentType.GetProperty("ContentForPackage", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
                FhirVersionLiteral = contentType.GetProperty("FhirVersionLiteral", typeof(string))?.GetValue(null, null) as string ?? string.Empty,
            };

            if (!info.ContentForPackage.Equals(currentRi, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (info.FhirVersionLiteral)
            {
                case "R4":
                    contentR4 = contentType;
                    break;

                case "R4B":
                    contentR4B = contentType;
                    break;

                case "R5":
                    contentR5 = contentType;
                    break;

                case "":
                    contentR4 = contentType;
                    contentR4B = contentType;
                    contentR5 = contentType;
                    break;
            }
        }
    }

}
