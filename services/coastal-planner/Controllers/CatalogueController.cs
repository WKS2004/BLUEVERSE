using Blueverse.CoastalPlanner.Authorization;
using Blueverse.CoastalPlanner.Integration;
using Microsoft.AspNetCore.Mvc;

namespace Blueverse.CoastalPlanner.Controllers;

[ApiController]
[Route("api/planner/catalogue")]
public sealed class CatalogueController(IPlannerCatalogueClient catalogue) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<PlannerCatalogueResult>> GetCatalogue(CancellationToken ct) =>
        Ok(await catalogue.GetDestinationsAsync(ct));
}
