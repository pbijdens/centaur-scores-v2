using CentaurScores.Api.Application;
using CentaurScores.Api.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace CentaurScores.Api.Controllers;

/// <summary>
/// Anonymous published results: a manager publishes a match or competition under a slug (PUT .../publication) and anyone
/// with the link can read it here. Published results are public but must never be indexed by search engines. Being anonymous
/// and (for competitions) compute intensive, it is rate limited per caller (PublicRateLimiting) and answered from a short-lived
/// cache (PublishedResultsService).
/// </summary>
[ApiController]
[AllowAnonymous]
[NoIndex]
[EnableRateLimiting(PublicRateLimiting.PublishedResultsPolicy)]
public sealed class PublishedResultsController(IPublishedResultsService publishedResultsService) : ControllerBase
{
    [HttpGet("published-results/{slug}")]
    public async Task<ActionResult<PublishedResults>> Get(string slug, CancellationToken cancellationToken)
    {
        var results = await publishedResultsService.FindAsync(slug, cancellationToken);
        return results is null ? NotFound() : Ok(results);
    }
}

/// <summary>Adds "X-Robots-Tag: noindex, nofollow" to every response of the decorated controller or action.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class NoIndexAttribute : ResultFilterAttribute
{
    public const string HeaderValue = "noindex, nofollow, noarchive";

    public override void OnResultExecuting(ResultExecutingContext context)
    {
        context.HttpContext.Response.Headers["X-Robots-Tag"] = HeaderValue;
        base.OnResultExecuting(context);
    }
}
