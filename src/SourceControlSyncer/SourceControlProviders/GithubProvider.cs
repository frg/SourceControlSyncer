using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SourceControlSyncer.SourceControls;

namespace SourceControlSyncer.SourceControlProviders
{
    public class GithubProvider : ISourceControlProvider
    {
        private const string ProviderName = "github";
        private const string DefaultRepoPathTemplate = "./repos/{ProviderName}/{Namespace}/{Slug}";
        private readonly string _accessToken;
        private readonly GitSourceControlAsync _gitSourceControl;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _username;

        public GithubProvider(ILogger logger, GitSourceControlAsync gitSourceControl, string username, string accessToken)
        {
            _logger = logger;
            _gitSourceControl = gitSourceControl;
            _username = username;
            _accessToken = accessToken;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/65.0.3325.181 Safari/537.36");
        }

        public Task<List<RepositoryInfo>> FetchRepositories(string[] reposMatchers)
        {
            _logger.Debug("Getting repositories for username {Username}...", _username);

            var repos = GetRepositories();

            _logger.Debug("Found {Count} repositories...", repos.Count);

            if (reposMatchers != null)
                repos = repos.Where(r =>
                        reposMatchers.Any(x => x.Equals(r.Name, StringComparison.InvariantCultureIgnoreCase)))
                    .ToList();

            _logger.Information(
                "Found {Count} repositories matching 1 of the following matchers {RepositoriesMatchers}...",
                repos.Count, reposMatchers ?? new string[0]);

            return Task.FromResult(repos);
        }

        public async Task EnsureRepositoriesSync(List<RepositoryInfo> repositories, string pathTemplate,
            string[] branchMatchers)
        {
            // Order repositories by non repositories first
            bool LocalRepositoriesFirst(RepositoryInfo repo) => _gitSourceControl.IsLocalRepository(GetRepositoryAbsolutePath(repo, pathTemplate));
            repositories = repositories.OrderBy(LocalRepositoriesFirst).ToList();

            var repositorySyncInfoList = repositories.Select(r => new RepositorySyncInfo
            {
                RemoteUrl = r.HttpHref,
                LocalRepositoryDirectory = GetRepositoryAbsolutePath(r, pathTemplate)
            });

            await _gitSourceControl.SyncRepositories(repositorySyncInfoList, branchMatchers, new CancellationToken());
        }

        private static string GetRepositoryAbsolutePath(RepositoryInfo repo, string pathTemplate = DefaultRepoPathTemplate)
        {
            if (string.IsNullOrEmpty(pathTemplate))
                pathTemplate = DefaultRepoPathTemplate;

            var relativeRepoPath = StringTemplate.Compile(pathTemplate, new Dictionary<string, string>
            {
                {"ProviderName", ProviderName},
                {"Namespace", repo.Namespace.ToLowerInvariant()},
                {"Slug", repo.Slug.ToLowerInvariant()}
            });

            return Path.GetFullPath(relativeRepoPath);
        }

        private List<RepositoryInfo> GetRepositories()
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.github.com/user/repos?access_token={_accessToken}");

            using (var res = _httpClient.SendAsync(req).GetAwaiter().GetResult())
            using (var content = res.Content)
            {
                var data = content.ReadAsStringAsync().GetAwaiter().GetResult();

                return ((JArray) JsonConvert.DeserializeObject<dynamic>(data))
                    .Select(x => new RepositoryInfo(
                        (string) x["name"],
                        (string) x["name"],
                        (string) x["owner"]["login"],
                        (string) x["clone_url"])
                    ).ToList();
            }
        }
    }

    public class GithubOrganizationInfo
    {
        public GithubOrganizationInfo(string name, string description)
        {
            Name = name;
            Description = description;
        }

        public string Name { get; }
        public string Description { get; }
    }
}