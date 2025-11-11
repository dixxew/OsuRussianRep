using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using OsuRussianRep.Interfaces;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class UsersController(IUsersService users) : ControllerBase
{
    /// <summary>
    /// sortField: "reputation" | "messages"
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetUsers(
        [Required] string sortField,
        int pageNumber = 1,
        int pageSize = 10,
        string? search = null,
        CancellationToken ct = default)
    {
        if (pageNumber <= 0 || pageSize <= 0)
            return BadRequest("Page number and page size must be greater than zero.");

        var result = await users.GetUsersAsync(sortField, pageNumber, pageSize, search, ct);
        return Ok(result);
    }
}