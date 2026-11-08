namespace ArticleService.Contracts;

public sealed record ArticleHeadline(
    Guid Id,
    string Title,
    string? Source,
    string Scope,
    DateTime CreatedAtUtc);
