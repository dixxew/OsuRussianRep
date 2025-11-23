using Microsoft.AspNetCore.Mvc;
using OsuRussianRep.Dtos;
using OsuRussianRep.Services;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public sealed class StatsController(IWordStatsService stats) : ControllerBase
{
    // GET api/stats/top-words?from=2025-10-01&to=2025-10-16&limit=100
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TopWordDto>>> GetTopWords(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        // дефолт: последние 7 дней [today-7, today)
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var f = from ?? new DateOnly(2007,1,1);
        var t = to   ?? today.AddDays(1);
        if (t <= f) return BadRequest("to must be > from");

        var data = await stats.GetTopWords(f, t, limit ?? 100, ct);
        return Ok(data);
    }

    // GET api/stats/word-series?word=привет&from=2025-10-01&to=2025-10-16
    [HttpGet]
    public async Task<ActionResult<IEnumerable<WordSeriesPointDto>>> GetWordSeries(
        [FromQuery] string word,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(word)) return BadRequest("word is required");

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var f = from ?? today.AddDays(-30);
        var t = to   ?? today.AddDays(1);
        if (t <= f) return BadRequest("to must be > from");

        var data = await stats.GetWordTimeseries(word, f, t, ct);
        return Ok(data);
    }

    // GET api/stats/words?q=pri&limit=10
    [HttpGet]
    public async Task<ActionResult<IEnumerable<string>>> SuggestWords(
        [FromQuery] string q,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        var data = await stats.SuggestWords(q, limit ?? 20, ct);
        return Ok(data);
    }
}