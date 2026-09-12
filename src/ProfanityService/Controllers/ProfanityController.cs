using Microsoft.AspNetCore.Mvc;
using ProfanityService.Contracts;
using ProfanityService.Services;

namespace ProfanityService.Controllers;

[ApiController]
[Route("api/profanity")]
public sealed class ProfanityController(IProfanityFilter filter) : ControllerBase
{
    [HttpPost("filter")]
    public async Task<ActionResult<FilterTextResponse>> Filter(
        FilterTextRequest request,
        CancellationToken cancellationToken)
    {
        var result = await filter.FilterAsync(request.Text, cancellationToken);
        return Ok(result);
    }
}
