using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Axiom.Atlas.Web.Model.Releases;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Axiom.Atlas.Web.Services.Releases;

public sealed class GitHubReleaseNotesService : IGitHubReleaseNotesService
{
    private const string CacheKey = "github-release-notes";
    private const int MaxFullChangelogRequests = 12;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);
    private static readonly Regex FullChangelogPattern = new(
        @"\*{0,2}Full\s+Changelog\*{0,2}\s*:\s*https?://github\.com/[^\s/]+/[^\s/]+/compare/(?<base>[^\s]+?)\.\.\.(?<head>[^\s\)]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ReleaseNotesOptions _options;
    private readonly ILogger<GitHubReleaseNotesService> _logger;

    public GitHubReleaseNotesService(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<ReleaseNotesOptions> options,
        ILogger<GitHubReleaseNotesService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ReleaseNotesViewModel> GetReleaseNotesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out ReleaseNotesViewModel? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                $"repos/{_options.Repository}/releases?per_page=30",
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var publishedReleases = (JsonSerializer.Deserialize<List<GitHubReleaseDto>>(content, JsonOptions) ?? [])
                .Where(release => !release.Draft)
                .ToList();

            var releases = new List<ReleaseNoteViewModel>(publishedReleases.Count);
            for (var index = 0; index < publishedReleases.Count; index++)
            {
                releases.Add(await MapReleaseAsync(
                    publishedReleases[index],
                    index < MaxFullChangelogRequests,
                    cancellationToken));
            }

            var model = new ReleaseNotesViewModel
            {
                Releases = releases
            };

            _cache.Set(CacheKey, model, CacheDuration);
            return model;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogWarning(exception, "Não foi possível obter as notas de versão.");

            return new ReleaseNotesViewModel
            {
                Notice = "Não foi possível atualizar as notas de versão agora. Tente novamente em alguns minutos."
            };
        }
    }

    private async Task<ReleaseNoteViewModel> MapReleaseAsync(
        GitHubReleaseDto release,
        bool readFullChangelog,
        CancellationToken cancellationToken)
    {
        var version = string.IsNullOrWhiteSpace(release.TagName) ? "Versão publicada" : release.TagName;
        var title = string.IsNullOrWhiteSpace(release.Name) || release.Name.Equals(release.TagName, StringComparison.OrdinalIgnoreCase)
            ? $"Atualização {version}"
            : release.Name;

        var changes = readFullChangelog
            ? await MapFullChangelogChangesAsync(release.Body, cancellationToken)
            : [];

        if (changes.Count == 0)
        {
            changes = MapChanges(release.Body).ToList();
        }

        return new ReleaseNoteViewModel
        {
            Version = version,
            Title = title,
            PublishedAt = release.PublishedAt,
            Changes = changes
        };
    }

    private async Task<List<ReleaseChangeViewModel>> MapFullChangelogChangesAsync(
        string? releaseBody,
        CancellationToken cancellationToken)
    {
        if (!TryGetComparisonReferences(releaseBody, out var baseReference, out var headReference))
        {
            return [];
        }

        try
        {
            using var response = await _httpClient.GetAsync(
                $"repos/{_options.Repository}/compare/{Uri.EscapeDataString(baseReference)}...{Uri.EscapeDataString(headReference)}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var comparison = JsonSerializer.Deserialize<GitHubComparisonDto>(content, JsonOptions);

            return (comparison?.Commits ?? [])
                .Select(commit => commit.Commit?.Message)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(message => message!.Split('\n', 2, StringSplitOptions.TrimEntries)[0])
                .Select(CleanChange)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Select(CreateFriendlyChange)
                .ToList();
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or TaskCanceledException)
        {
            _logger.LogInformation(exception, "Não foi possível consultar o histórico detalhado de uma release.");
            return [];
        }
    }

    private static bool TryGetComparisonReferences(string? releaseBody, out string baseReference, out string headReference)
    {
        baseReference = string.Empty;
        headReference = string.Empty;

        var match = FullChangelogPattern.Match(releaseBody ?? string.Empty);
        if (!match.Success)
        {
            return false;
        }

        baseReference = match.Groups["base"].Value;
        headReference = match.Groups["head"].Value;
        return !string.IsNullOrWhiteSpace(baseReference) && !string.IsNullOrWhiteSpace(headReference);
    }

    private static IReadOnlyList<ReleaseChangeViewModel> MapChanges(string? body)
    {
        var changeLines = (body ?? string.Empty)
            .Replace("\r", string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("- ", StringComparison.Ordinal))
            .Select(line => CleanChange(line[2..]))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (changeLines.Count == 0)
        {
            return
            [
                new ReleaseChangeViewModel
                {
                    Category = "Manutenção",
                    Description = "Atualização de estabilidade e evolução contínua do Axiom Atlas.",
                    BadgeClass = "badge-light-primary"
                }
            ];
        }

        return changeLines.Select(CreateFriendlyChange).ToList();
    }

    private static string CleanChange(string value)
    {
        var withoutAuthor = Regex.Replace(value, @"\s+by\s+@.*$", string.Empty, RegexOptions.IgnoreCase);
        var withoutPullRequest = Regex.Replace(withoutAuthor, @"\s+in\s+https?://\S+$", string.Empty, RegexOptions.IgnoreCase);
        var withoutLinks = Regex.Replace(
            withoutPullRequest,
            @"\[(?<label>[^\]]+)\]\([^)]+\)",
            match => match.Groups["label"].Value);
        var withoutMarkdown = withoutLinks.Replace("**", string.Empty).Trim();

        return withoutMarkdown.Equals("Full Changelog", StringComparison.OrdinalIgnoreCase)
            || withoutMarkdown.StartsWith("Full Changelog:", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : withoutMarkdown;
    }

    private static ReleaseChangeViewModel CreateFriendlyChange(string description)
    {
        var normalized = description.ToLowerInvariant();
        var (category, badgeClass) = normalized switch
        {
            _ when normalized.StartsWith("feat:") || normalized.Contains("novo recurso") => ("Novo recurso", "badge-light-success"),
            _ when normalized.StartsWith("fix:") || normalized.Contains("corrige") || normalized.Contains("correção") => ("Correção", "badge-light-danger"),
            _ when normalized.StartsWith("docs:") => ("Documentação", "badge-light-info"),
            _ when normalized.StartsWith("chore:") || normalized.StartsWith("refactor:") || normalized.StartsWith("build:") || normalized.StartsWith("ci:") => ("Manutenção", "badge-light-primary"),
            _ => ("Melhoria", "badge-light-warning")
        };

        var cleanDescription = Regex.Replace(description, @"^(feat|fix|docs|chore|refactor|build|ci):\s*", string.Empty, RegexOptions.IgnoreCase).Trim();

        return new ReleaseChangeViewModel
        {
            Category = category,
            Description = BuildFriendlyDescription(category, cleanDescription),
            BadgeClass = badgeClass
        };
    }

    private static string BuildFriendlyDescription(string category, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return "Aprimoramentos internos para manter o Axiom Atlas estável e confiável.";
        }

        var normalized = description.ToLowerInvariant();
        if (normalized.StartsWith("adiciona ") || normalized.StartsWith("inclui ") || normalized.StartsWith("corrige ")
            || normalized.StartsWith("melhora ") || normalized.StartsWith("atualiza ") || normalized.StartsWith("remove "))
        {
            return char.ToUpperInvariant(description[0]) + description[1..];
        }

        return category switch
        {
            "Novo recurso" => $"Inclui {description}.",
            "Correção" => $"Corrige {description}.",
            "Documentação" => $"Atualiza a documentação sobre {description}.",
            "Manutenção" => $"Aprimora a manutenção interna: {description}.",
            _ => $"Melhora {description}."
        };
    }

    private sealed class GitHubReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; init; }

        [JsonPropertyName("body")]
        public string? Body { get; init; }

        [JsonPropertyName("draft")]
        public bool Draft { get; init; }
    }

    private sealed class GitHubComparisonDto
    {
        [JsonPropertyName("commits")]
        public List<GitHubCommitDto>? Commits { get; init; }
    }

    private sealed class GitHubCommitDto
    {
        [JsonPropertyName("commit")]
        public GitHubCommitDetailsDto? Commit { get; init; }
    }

    private sealed class GitHubCommitDetailsDto
    {
        [JsonPropertyName("message")]
        public string? Message { get; init; }
    }
}
