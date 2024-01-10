// <copyright file="CDexTaskProcess.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>


using FhirCandle.Interactions;
using FhirCandle.Models;
using FhirCandle.Storage;
using Hl7.Fhir.Model;
using System.Net;

namespace FhirCandle.R4.InteractionHooks;

public class CDexTaskProcess : IFhirInteractionHook
{
    public string Name => "DaVinci CDex Task Process Hook";

    public string Id => "036a8204-4d4f-46fc-a715-900bc2790a16";

    public HashSet<TenantConfiguration.SupportedFhirVersions> SupportedFhirVersions => new()
    {
        TenantConfiguration.SupportedFhirVersions.R4,
    };

    public string RequiresPackage => "hl7.fhir.us.davinci-cdex";

    public Dictionary<string, HashSet<Common.StoreInteractionCodes>> InteractionsByResource => new()
    {
        { "Task", new() {
            Common.StoreInteractionCodes.TypeCreate,
            Common.StoreInteractionCodes.TypeCreateConditional,
            Common.StoreInteractionCodes.InstanceUpdate,
            Common.StoreInteractionCodes.InstanceUpdateConditional
        } },
    };

    public HashSet<Common.HookRequestStateCodes> HookRequestStates => new()
    {
        Common.HookRequestStateCodes.Post,
    };

    public bool Enabled { get => true; set => throw new NotImplementedException(); }

    /// <summary>Executes the interaction hook operation.</summary>
    /// <param name="ctx">          The context.</param>
    /// <param name="store">        The store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="resource">     The resource.</param>
    /// <param name="hookResponse"> [out] The hook response.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool DoInteractionHook(
        FhirRequestContext ctx, 
        VersionedFhirStore store, 
        IVersionedResourceStore? resourceStore, 
        Resource? resource,
        out FhirResponseContext hookResponse)
    {
        if (!ctx.ResourceType.Equals("Task"))
        {
            hookResponse = new();
            return false;
        }

        hookResponse = new();
        return false;
    }
}
