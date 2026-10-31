namespace NewsletterService.Controllers;

public sealed record NewsletterSendRequest(string? Subject, string? Body);
