using System;
using System.Collections.Concurrent;
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
    public class BitbucketServerProvider : ISourceControlProvider
    {
        private const string RestApiSuffix = "/rest/api/1.0";
        private const string ApiProjects = "/projects";
        private const string ApiRepositories = "/repos";

        private const string ProviderName = "bitbucket";
        private const string ProviderType = "server";
        private const string DefaultRepoPathTemplate = "./source/{ProviderName}/{ProviderType}/{Namespace}/{Slug}";
        private readonly string _bitbucketServerUrl;
        private readonly GitSourceControlAsync _gitSourceControl;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;
        private readonly string _username;

        public BitbucketServerProvider(ILogger logger, GitSourceControlAsync gitSourceControl,
            string bitbucketServerUrl,
            string username, string password)
        {
            _logger = logger;
            _gitSourceControl = gitSourceControl;
            _bitbucketServerUrl = bitbucketServerUrl;
            _username = username;

            _httpClient = new HttpClient();

            var basicAuthHeaderValue = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
            _httpClient.DefaultRequestHeaders.Authorization = basicAuthHeaderValue;
        }

        public async Task<List<RepositoryInfo>> FetchRepositories(string[] reposMatchers)
        {
            _logger.Debug("Getting projects for username {Username}...", _username);

            var projects = GetProjects();

            _logger.Debug("Found {Count} projects...", projects.Count);

            // Initiate all the API requests
            var concurrentBag = new ConcurrentBag<RepositoryInfo>();
            foreach (var project in projects)
            {
                await Task.Run(async () =>
                {
                    _logger.Debug("Getting repositories for project {ProjectName}", project.Name);
                    var repos = await GetRepositories(project.Key);
                    _logger.Debug("Found {Count} repositories for project {ProjectName}", repos.Count, project.Name);

                    foreach (var repo in repos)
                    {
                        concurrentBag.Add(repo);
                    }
                });
            }

            var list = concurrentBag.ToList();

            list = FilterRepositoriesByMatchers(list, reposMatchers);

            _logger.Information(
                "Found {Count} repositories matching 1 of the following matchers {RepositoriesMatchers}...",
                list.Count, reposMatchers ?? new string[0]);

            return list;
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

        private static string GetRepositoryAbsolutePath(RepositoryInfo repo,
            string pathTemplate = DefaultRepoPathTemplate)
        {
            if (string.IsNullOrEmpty(pathTemplate))
                pathTemplate = DefaultRepoPathTemplate;

            var relativeRepoPath = StringTemplate.Compile(pathTemplate, new Dictionary<string, string>
            {
                {"ProviderName", ProviderName},
                {"ProviderType", ProviderType},
                {"Namespace", repo.Namespace.ToLowerInvariant()},
                {"Slug", repo.Slug.ToLowerInvariant()}
            });

            return Path.GetFullPath(relativeRepoPath);
        }

        private async Task<List<RepositoryInfo>> GetRepositories(string projectKey)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{_bitbucketServerUrl}{RestApiSuffix}{ApiProjects}/{projectKey}{ApiRepositories}?limit=1000");

            using (var response = await _httpClient.SendAsync(req))
            using (var content = response.Content)
            {
                var data = await content.ReadAsStringAsync();

                return ((JArray) JsonConvert.DeserializeObject<dynamic>(data).values)
                    .Select(x => new RepositoryInfo(
                        (string) x["name"],
                        (string) x["slug"],
                        projectKey,
                        (string) x["links"]["clone"]
                            .Where(y => string.Equals((string) y["name"], "http"))
                            .Select(y => y["href"])
                            .First())
                    ).ToList();
            }
        }

        private List<BitbucketProjectInfo> GetProjects()
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"{_bitbucketServerUrl}{RestApiSuffix}{ApiProjects}?limit=1000");

            using (var res = _httpClient.SendAsync(req).GetAwaiter().GetResult())
            using (var content = res.Content)
            {
                var data = content.ReadAsStringAsync().GetAwaiter().GetResult();

                // TODO: fix page limit
                return ((JArray) JsonConvert.DeserializeObject<dynamic>(data).values)
                    .Select(x => new BitbucketProjectInfo(
                        (string) x["key"],
                        (string) x["name"],
                        (string) x["links"]["self"][0]["href"])
                    ).ToList();
            }
        }
    }
}