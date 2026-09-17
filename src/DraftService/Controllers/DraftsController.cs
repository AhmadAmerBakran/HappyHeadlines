using DraftService.Contracts;
using DraftService.Data;
using DraftService.Models;
using Microsoft.AspNetCore.Mvc;

namespace DraftService.Controllers;

[ApiController]
[Route("api/drafts")]
public sealed class DraftsController : ControllerBase
{
    private readonly IDraftRepository _repository;
    private readonly ILogger<DraftsController> _logger;

    public DraftsController(IDraftRepository repository, ILogger<DraftsController> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Draft>>> GetAll(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Fetching drafts");
        var drafts = await _repository.GetAllAsync(cancellationToken);
        return Ok(drafts);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Draft>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var draft = await _repository.GetByIdAsync(id, cancellationToken);
        return draft is null ? NotFound() : Ok(draft);
    }

    [HttpPost]
    public async Task<ActionResult<Draft>> Create(
        CreateDraftRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var draft = new Draft
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content,
            Author = request.Author.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _logger.LogInformation("Saving a new draft {DraftId} for {Author}", draft.Id, draft.Author);
        await _repository.CreateAsync(draft, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = draft.Id }, draft);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Draft>> Update(
        Guid id,
        UpdateDraftRequest request,
        CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            _logger.LogWarning("Draft {DraftId} was not found for update", id);
            return NotFound();
        }

        existing.Title = request.Title.Trim();
        existing.Content = request.Content;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await _repository.UpdateAsync(existing, cancellationToken);
        return Ok(existing);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await _repository.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            _logger.LogWarning("Draft {DraftId} was not found for deletion", id);
            return NotFound();
        }

        return NoContent();
    }
}
