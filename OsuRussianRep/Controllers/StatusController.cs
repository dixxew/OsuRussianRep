using Microsoft.AspNetCore.Mvc;
using OsuRussianRep.Interfaces;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class StatusController(IIrcService ircService) : Controller
{
    [HttpGet]
    public IActionResult IrcStatus()
    {        
        return Ok(ircService.IsConnected);
    }
}
