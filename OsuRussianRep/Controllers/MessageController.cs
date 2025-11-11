using Microsoft.AspNetCore.Mvc;
using OsuRussianRep.Interfaces;
using OsuRussianRep.Services;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class MessageController(MessageService messageService) : ControllerBase
{
    //[HttpGet("UserMessages")]
    //public async Task<ActionResult<IEnumerable<ChatUser>>> GetUserMessages(Guid userId, int pageNumber = 1, int pageSize = 10)
    //{
    //    if (pageNumber <= 0 || pageSize <= 0)        
    //        return BadRequest("Page number and page size must be greater than zero.");
        

    //    var totalRecords = await _context.Messages.Where(m => m.UserId == userId).CountAsync();

    //    var messages = await _context.Messages
    //        .Where(m => m.UserId == userId)
    //        .Skip((pageNumber - 1) * pageSize)
    //        .Take(pageSize)
    //        .ToListAsync();


    //    var response = new
    //    {
    //        TotalRecords = totalRecords,
    //        PageNumber = pageNumber,
    //        PageSize = pageSize,
    //        TotalPages = (int)Math.Ceiling((double)totalRecords / pageSize),
    //        Data = messages
    //    };

    //    return Ok(response);
    //}
    [HttpGet]
    public IActionResult GetMonthlyMessageCounts()
    {
        var messageCounts = messageService.GetMessageCountsForLast30Days();
        return Ok(messageCounts);
    }

    [HttpGet]
    public IActionResult GetDailyMessageCounts()
    {
        var messageCounts = messageService.GetHourlyAverageMessageCountsForLast30Days();
        return Ok(messageCounts);
    }
}
