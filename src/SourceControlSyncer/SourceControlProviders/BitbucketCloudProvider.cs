using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SourceControlSyncer.SourceControls;

namespace SourceControlSyncer.SourceControlProviders
{
    public class BitbucketCloudProvider : ISourceControlProvider
    {
        private const string ProviderName = "bitbucket";
        private const string ProviderType = "cloud";
        private const string DefaultRepoPathTemplate = "./source/{ProviderName}/{ProviderType}/{AccountUsername}/{Namespace}/{Slug}";
        private readonly string _accountUsername;
        private readonly GitSourceControlAsync _gitSourceControl;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public BitbucketCloudProvider(ILogger logger, GitSourceControlAsync gitSourceControl, string accountUsername,
            string username, string password)
        {
            _logger = logger;
            _gitSourceControl = gitSourceControl;
            _accountUsername = accountUsername;

            _httpClient = new HttpClient();

            var basicAuthHeaderValue = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
            _httpClient.DefaultRequestHeaders.Authorization = basicAuthHeaderValue;
        }

        public async Task<List<RepositoryInfo>> FetchRepositories(string[] reposMatchers)
        {
            _logger.Debug("Getting repositories for username {AccountUsername}", _accountUsername);
            var repositories = await GetRepositories();
            _logger.Debug("Found {Count} repositories for username {AccountUsername}", repositories.Count,
                _accountUsername);

            repositories = FilterRepositoriesByMatchers(repositories, reposMatchers);

            _logger.Information(
                "Found {Count} repositories matching 1 of the following matchers {RepositoriesMatchers}...",
                repositories.Count, reposMatchers ?? new string[0]);

            return repositories;
        }

        public async Task EnsureRepositoriesSync(List<RepositoryInfo> repositories,
            string pathTemplate, string[] branchMatchers)
        {
            // Order repositories by non repositories first
            bool LocalRepositoriesFirst(RepositoryInfo repo)
            {
                return _gitSourceControl.IsLocalRepository(GetRepositoryAbsolutePath(repo, pathTemplate));
            }

            repositories = repositories.OrderBy(LocalRepositoriesFirst).ToList();

            var repositorySyncInfoList = repositories.Select(r => new RepositorySyncInfo
            {
                RemoteUrl = r.HttpHref,
                LocalRepositoryDirectory = GetRepositoryAbsolutePath(r, pathTemplate)
            });

            await _gitSourceControl.SyncRepositories(repositorySyncInfoList, branchMatchers, new CancellationToken());
        }

        private static List<RepositoryInfo> FilterRepositoriesByMatchers(List<RepositoryInfo> repositories,
            string[] reposMatchers)
        {
            if (reposMatchers != null && reposMatchers.Any())
            {
                var rgxReposMatchers = reposMatchers.Select(x =>
                    new Regex(x, RegexOptions.Compiled | RegexOptions.IgnoreCase));

                repositories = repositories
                    .Where(repo => rgxReposMatchers
                        .Any(regex => regex.Matches(repo.Name).Count > 0))
                    .ToList();
            }

            return repositories;
        }

        private string GetRepositoryAbsolutePath(RepositoryInfo repo, string pathTemplate = DefaultRepoPathTemplate)
        {
            if (string.IsNullOrEmpty(pathTemplate))
                pathTemplate = DefaultRepoPathTemplate;

            var relativeRepoPath = StringTemplate.Compile(pathTemplate, new Dictionary<string, string>
            {
                {"ProviderName", ProviderName},
                {"ProviderType", ProviderType},
                {"AccountUsername", _accountUsername},
                {"Namespace", repo.Namespace.ToLowerInvariant()},
                {"Slug", repo.Slug.ToLowerInvariant()}
            });

            return Path.GetFullPath(relativeRepoPath);
        }

        private async Task<List<RepositoryInfo>> GetRepositories()
        {
            var repositories = new List<RepositoryInfo>();

            async Task MakeRequest(string requestUri)
            {
                var req = new HttpRequestMessage(HttpMethod.Get, requestUri);

                using (var response = await _httpClient.SendAsync(req))
                using (var content = response.Content)
                {
                    var data = await content.ReadAsStringAsync();
                    var deserializedResp = JObject.Parse(data);
                    var reposInfo = ((JArray)deserializedResp["values"])
                        .Select(x => new RepositoryInfo(
                            (string) x["name"],
                            (string) x["slug"],
                            (string) x["project"]["key"],
                            (string) x["links"]["clone"]
                                .Where(y => string.Equals((string) y["name"], "https"))
                                .Select(y => y["href"])
                                .First())
                        ).ToList();
                    repositories.AddRange(reposInfo);

                    // Get the next set of repositories
                    if (deserializedResp.ContainsKey("next"))
                    {
                        var nextUri = deserializedResp["next"].ToString();
                        if (!string.IsNullOrEmpty(nextUri))
                        {
                            await MakeRequest(nextUri);
                        }
                    }
                }
            }

            await MakeRequest($"https://api.bitbucket.org/2.0/repositories/{_accountUsername}?pagelen=100");

            return repositories;
        }
    }
}