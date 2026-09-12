namespace ArticleService.Data;

public interface IArticleShardResolver
{
    string GetConnectionString(string scope);
    IReadOnlyCollection<string> GetAllConnectionStrings();
}
