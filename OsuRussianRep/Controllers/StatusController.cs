using Microsoft.AspNetCore.Mvc;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class StatusController() : Controller
{
    [HttpGet]
    public IActionResult IrcStatus()
    {        
        return Ok();
    }
}
