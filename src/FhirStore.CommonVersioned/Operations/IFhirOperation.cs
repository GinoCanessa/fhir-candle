using System;
using System.Net;

namespace FhirStore.Operations;

public interface IFhirOperation
{
    string OperationName { get; }
    string OperationCanonical { get; }

    HashSet<FhirStore.Models.TenantConfiguration.SupportedFhirVersions> FhirVersions { get; }

    bool AllowGet { get; }
    bool AllowPost { get; }

    bool AllowSystemLevel { get; }
    bool AllowResourceLevel { get; }
    bool AllowInstanceLevel { get; }

    HashSet<string> SupportedResources { get; }

    public HttpStatusCode DoOperation(
        Storage.VersionedFhirStore store,
        string resourceType,
        Storage.IVersionedResourceStore? resourceStore,
        string instanceId,
        string queryString,
        Hl7.Fhir.Model.Resource? bodyResource,
        out Hl7.Fhir.Model.Resource? responseResource,
        out Hl7.Fhir.Model.OperationOutcome? responseOutcome,
        out string contentLocation);
}

