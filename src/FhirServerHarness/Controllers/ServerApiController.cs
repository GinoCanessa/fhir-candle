using System;
using FhirServerHarness.Services;
using FhirStore.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace FhirServerHarness.Controllers;

[ApiController]
[Route("api/server")]
[Produces("application/json")]
public class ServerApiController : ControllerBase
{
	private IFhirStoreManager _fhirStore;

	public ServerApiController([FromServices] IFhirStoreManager fhirStoreManager)
	{
		_fhirStore = fhirStoreManager;
	}

	[HttpGet, Route("tenants")]
	public IActionResult GetFhirTenantList()
	{
		IEnumerable<ProviderConfiguration> configs = _fhirStore.Select(kvp => kvp.Value.Config);

		return Ok(configs);
	}
}

