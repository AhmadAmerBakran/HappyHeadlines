namespace HappyHeadlines.Messaging;

public static class ArticleMessaging
{
    public const string Exchange = "happyheadlines.articles.published";
    public const string ArticleServiceQueue = "happyheadlines.article-service.published";
    public const string NewsletterServiceQueue = "happyheadlines.newsletter-service.published";
}
