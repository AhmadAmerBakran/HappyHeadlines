using System.Text.RegularExpressions;
using ProfanityService.Contracts;
using ProfanityService.Data;

namespace ProfanityService.Services;

public sealed class ProfanityFilter(IProfanityRepository repository) : IProfanityFilter
{
    public async Task<FilterTextResponse> FilterAsync(
        string text,
        CancellationToken cancellationToken)
    {
        var words = await repository.GetWordsAsync(cancellationToken);
        var filteredText = text;
        var hadProfanity = false;

        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }

            var pattern = $@"(?<![\p{{L}}\p{{N}}_]){Regex.Escape(word)}(?![\p{{L}}\p{{N}}_])";
            var replacement = new string('*', word.Length);
            var updated = Regex.Replace(
                filteredText,
                pattern,
                replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!string.Equals(updated, filteredText, StringComparison.Ordinal))
            {
                hadProfanity = true;
                filteredText = updated;
            }
        }

        return new FilterTextResponse
        {
            FilteredText = filteredText,
            HadProfanity = hadProfanity
        };
    }
}
