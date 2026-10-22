using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CentaurScores.Api.Controllers;

// External results endpoint (documentation/FIND-MATCHES.md). Deliberately NOT derived from ApiControllerBase:
// that base rejects tokens without a selected tenant, but external systems call this with the plain token from
// POST api/auth/login (tenant_id = Guid.Empty). Tenant scoping is done by the service instead, which searches
// every tenant the account is authorized for. The route shares the api/matches prefix with MatchesController;
// the literal "find" segment cannot collide with its {id:guid} routes.
[ApiController]
[Authorize]
[Route("api/matches")]
public sealed class MatchFindController(IMatchFindService matchFindService, ITenantContext tenantContext) : ControllerBase
{
    [HttpPost("find")]
    public async Task<IActionResult> Find(FindMatchRequest request, CancellationToken cancellationToken)
    {
        var result = await matchFindService.FindAsync(tenantContext.AccountId, request, cancellationToken);
        return result.Outcome switch
        {
            MatchFindOutcome.Found => Ok(result.Match),
            MatchFindOutcome.NotFound => NotFound(result.Error),
            MatchFindOutcome.Ambiguous => Conflict(result.Error),
            _ => BadRequest(result.Error)
        };
    }
}
