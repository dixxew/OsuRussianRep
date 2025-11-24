using Microsoft.AspNetCore.Mvc;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuRussianRep.Services;

namespace OsuRussianRep.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class UserWordStatsController(IUserWordStatsService stats, AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Возвращает топ слов для пользователя
    /// </summary>
    [HttpGet("{nickname}")]
    public async Task<ActionResult<IReadOnlyList<TopWordDto>>> TopWords(string nickname, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(nickname))
            return BadRequest("nickname is required");

        var words = await stats.GetTopWordsForUser(nickname, limit, ct);

        if (words.Count == 0)
            return NotFound($"No words found for '{nickname}'");

        return Ok(words);
    }

    /// <summary>
    /// Возвращает топ юзеров, чаще всего использующих указанное слово
    /// </summary>
    [HttpGet("{lemma}")]
    public async Task<ActionResult<IReadOnlyList<object>>> TopUsers(string lemma, int limit = 20, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lemma))
            return BadRequest("lemma is required");

        var users = await stats.GetUsersForWord(lemma, limit, ct);
        if (users.Count == 0)
            return NotFound($"No users for word '{lemma}'");

        // Немного приукрасим DTO, чтобы не светить tuple
        return Ok(users.Select(u => new { Nickname = u.Lemma, Count = u.Count }));
    }
}