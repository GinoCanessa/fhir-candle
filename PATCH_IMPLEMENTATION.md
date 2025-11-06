# FHIR PATCH Implementation for fhir-candle

## Overview

This document describes the implementation of HTTP PATCH support for fhir-candle to address the limitation discovered during testing where partial updates are not supported.

## Current Limitation

**Problem**: Server requires complete resource on PUT operations. Cannot update just a single field like `status`.

**Impact**:
- Requires read-before-write pattern
- More bandwidth usage
- Potential race conditions
- Poor developer experience

## Solution: Add PATCH Support

Implement HTTP PATCH with JSON Patch (RFC 6902) support as per FHIR specification.

## Implementation Steps

### 1. Add JSON Patch Library Dependency

Add to `fhir-candle.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.JsonPatch" Version="8.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

### 2. Add PATCH Controller Method

Add to `FhirController.cs`:

```csharp
/// <summary>(An Action that handles HTTP PATCH requests) patches resource instance.</summary>
/// <param name="storeName">   The store.</param>
/// <param name="resourceName">Name of the resource.</param>
/// <param name="id">          The identifier.</param>
/// <param name="format">      Describes the format to use.</param>
/// <param name="pretty">      The pretty.</param>
/// <param name="prefer">      The prefer.</param>
/// <param name="ifMatch">     A match specifying if.</param>
/// <param name="authHeader">  The authentication header.</param>
/// <returns>An asynchronous result.</returns>
[HttpPatch, Route("{storeName}/{resourceName}/{id}")]
[Consumes("application/json-patch+json")]
public async Task PatchResourceInstance(
    [FromRoute] string storeName,
    [FromRoute] string resourceName,
    [FromRoute] string id,
    [FromQuery(Name = "_format")] string? format,
    [FromQuery(Name = "_pretty")] string? pretty,
    [FromHeader(Name = "Prefer")] string? prefer,
    [FromHeader(Name = "If-Match")] string? ifMatch,
    [FromHeader(Name = "Authorization")] string? authHeader)
{
    if (!_fhirStoreManager.TryGetValue(storeName, out IFhirStore? store))
    {
        await LogAndReturnError(Response, 404, $"PatchResourceInstance <<< no tenant at {storeName}!");
        return;
    }

    if (!store.SupportsResource(resourceName))
    {
        await LogAndReturnError(Response, 404, $"PatchResourceInstance <<< tenant {storeName} does not support resource {resourceName}!");
        return;
    }

    try
    {
        // Read the PATCH body (JSON Patch document)
        using StreamReader reader = new StreamReader(Request.Body);
        string patchContent = await reader.ReadToEndAsync();

        // Step 1: Retrieve the current resource
        FhirRequestContext getCtx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = "GET",
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            Interaction = Common.StoreInteractionCodes.InstanceRead,
            ResourceType = resourceName,
            Id = id,
        };

        if (!store.InstanceRead(getCtx, out FhirResponseContext getResponse))
        {
            // Resource not found or other error
            await AddFhirResponse(Response, prefer, false, getResponse);
            return;
        }

        // Step 2: Apply JSON Patch to the resource
        string currentResourceJson = getResponse.SerializedResource ?? string.Empty;

        if (string.IsNullOrEmpty(currentResourceJson))
        {
            await LogAndReturnError(Response, 500, "PatchResourceInstance <<< current resource is empty!");
            return;
        }

        // Parse and apply the patch
        var patchDoc = JsonConvert.DeserializeObject<JsonPatchDocument>(patchContent);
        var resourceObject = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(currentResourceJson);

        if (patchDoc == null || resourceObject == null)
        {
            await LogAndReturnError(Response, 400, "PatchResourceInstance <<< invalid patch document or resource!");
            return;
        }

        // Apply the patch operations
        try
        {
            patchDoc.ApplyTo(resourceObject);
        }
        catch (Exception patchEx)
        {
            await LogAndReturnError(Response, 400, $"PatchResourceInstance <<< patch apply failed: {patchEx.Message}");
            return;
        }

        // Convert back to string
        string patchedResourceJson = JsonConvert.SerializeObject(resourceObject);

        // Step 3: Update the resource with patched content
        FhirRequestContext updateCtx = new()
        {
            TenantName = storeName,
            Store = store,
            HttpMethod = "PUT", // Use PUT internally for the update
            Url = Request.GetDisplayUrl(),
            UrlPath = Request.Path,
            UrlQuery = Request.QueryString.ToString(),
            RequestHeaders = Request.Headers.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            Authorization = _smartAuthManager.GetAuthorization(storeName, authHeader ?? string.Empty),
            DestinationFormat = GetMimeType(format, Request),
            SerializePretty = pretty?.Equals("true", StringComparison.Ordinal) ?? false,
            Interaction = Common.StoreInteractionCodes.InstanceUpdate,
            ResourceType = resourceName,
            Id = id,
            IfMatch = ifMatch ?? string.Empty,
            SourceFormat = "application/fhir+json",
            SourceContent = patchedResourceJson,
        };

        if (!_smartAuthManager.IsAuthorized(updateCtx))
        {
            Response.StatusCode = 401;
            return;
        }

        bool success = store.InstanceUpdate(
            updateCtx,
            out FhirResponseContext opResponse);

        await AddFhirResponse(Response, prefer, success, opResponse);
    }
    catch (Exception ex)
    {
        string msg = ex.InnerException == null
            ? $"PatchResourceInstance <<< caught: {ex.Message}"
            : $"PatchResourceInstance <<< caught: {ex.Message}, inner: {ex.InnerException.Message}";
        await LogAndReturnError(Response, 500, msg);
        return;
    }
}
```

### 3. Required Using Statements

Add to top of `FhirController.cs`:

```csharp
using Microsoft.AspNetCore.JsonPatch;
using Newtonsoft.Json;
```

## Usage Example

### Before (Required Full Resource):

```http
PUT /fhir/r4/DocumentReference/abc
Content-Type: application/fhir+json

{
  "resourceType": "DocumentReference",
  "id": "abc",
  "identifier": [...],
  "status": "entered-in-error",
  "type": {...},
  "category": [...],
  "subject": {...},
  "date": "...",
  "author": [...],
  "content": [...]
}
```

### After (PATCH with Single Field):

```http
PATCH /fhir/r4/DocumentReference/abc
Content-Type: application/json-patch+json

[
  {
    "op": "replace",
    "path": "/status",
    "value": "entered-in-error"
  }
]
```

## JSON Patch Operations Supported

Per RFC 6902:

- **replace**: Replace a value
  ```json
  {"op": "replace", "path": "/status", "value": "entered-in-error"}
  ```

- **add**: Add a new field or array element
  ```json
  {"op": "add", "path": "/category/-", "value": {"coding": [...]}}
  ```

- **remove**: Remove a field
  ```json
  {"op": "remove", "path": "/description"}
  ```

- **move**: Move a value
  ```json
  {"op": "move", "from": "/author/0", "path": "/author/1"}
  ```

- **copy**: Copy a value
  ```json
  {"op": "copy", "from": "/subject", "path": "/custodian"}
  ```

- **test**: Test a value (validation)
  ```json
  {"op": "test", "path": "/status", "value": "current"}
  ```

## Testing the Implementation

### Test 1: Simple Status Update

```bash
# Get current resource
GET /fhir/r4/DocumentReference/abc

# Patch status
PATCH /fhir/r4/DocumentReference/abc
Content-Type: application/json-patch+json

[
  {"op": "replace", "path": "/status", "value": "entered-in-error"}
]

# Expected: 200 OK with updated resource
```

### Test 2: Add Category

```bash
PATCH /fhir/r4/DocumentReference/abc
Content-Type: application/json-patch+json

[
  {
    "op": "add",
    "path": "/category/-",
    "value": {
      "coding": [{
        "system": "http://example.org/category",
        "code": "additional"
      }]
    }
  }
]
```

### Test 3: Multiple Operations

```bash
PATCH /fhir/r4/DocumentReference/abc
Content-Type: application/json-patch+json

[
  {"op": "test", "path": "/status", "value": "current"},
  {"op": "replace", "path": "/status", "value": "entered-in-error"},
  {"op": "add", "path": "/description", "value": "Corrected via PATCH"}
]
```

## Benefits

1. **Bandwidth Reduction**: Only send changed fields
2. **Race Condition Mitigation**: Atomic operations with `test` operation
3. **Better Developer Experience**: Simple, focused updates
4. **FHIR Compliance**: Aligns with FHIR R4 specification
5. **Backward Compatible**: Existing PUT operations still work

## Implementation Notes

### Security Considerations

1. **Authorization**: Same authorization checks as PUT
2. **Validation**: Full resource validation after patch application
3. **If-Match**: Supports optimistic concurrency control
4. **Audit**: Patch operations logged same as updates

### Error Handling

**400 Bad Request**: Invalid patch document or operation
**404 Not Found**: Resource doesn't exist
**409 Conflict**: If-Match version mismatch
**422 Unprocessable Entity**: Patched resource fails validation

### Performance

- GET + PATCH + PUT in single request
- In-memory patch application (fast)
- No additional database queries

## Alternative: FHIR Patch

FHIR also defines FHIRPath-based patch (Parameters resource). This could be added as future enhancement:

```json
{
  "resourceType": "Parameters",
  "parameter": [
    {
      "name": "operation",
      "part": [
        {"name": "type", "valueCode": "replace"},
        {"name": "path", "valueString": "DocumentReference.status"},
        {"name": "value", "valueCode": "entered-in-error"}
      ]
    }
  ]
}
```

## Deployment

1. Update `fhir-candle.csproj` with dependencies
2. Add PATCH method to `FhirController.cs`
3. Build: `dotnet build`
4. Test locally
5. Deploy to subscriptions.argo.run

## Documentation Updates

Update server capabilities to advertise PATCH support:

```json
{
  "resourceType": "CapabilityStatement",
  "rest": [{
    "mode": "server",
    "resource": [{
      "type": "DocumentReference",
      "interaction": [
        {"code": "read"},
        {"code": "create"},
        {"code": "update"},
        {"code": "patch"},  // <-- ADD THIS
        {"code": "delete"}
      ]
    }]
  }]
}
```

## Conclusion

This PATCH implementation addresses the partial update limitation discovered in testing, bringing fhir-candle to full FHIR R4 compliance and significantly improving developer experience.

**Status**: Ready for implementation
**Effort**: ~2-3 hours (including testing)
**Impact**: High (resolves major usability issue)
