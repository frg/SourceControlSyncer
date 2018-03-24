using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

namespace SourceControlSyncer
{
    public class BitbucketProvider : ISourceControlProvider
    {
        private readonly ILogger _logger;
        private readonly GitSourceControl _gitSourceControl;
        private readonly string _bitbucketServerUrl;
        private readonly string _username;
        private readonly HttpClient _httpClient;
        private const string RestApiSuffix = "/rest/api/1.0";
        private const string ApiProjects = "/projects";
        private const string ApiRepositories = "/repos";
        
        private const string ProviderName = "bitbucket";
        private const string DefaultRepoPathTemplate = "./repos/{ProviderName}/{Namespace}/{Slug}";

        public BitbucketProvider(ILogger logger, GitSourceControl gitSourceControl, string bitbucketServerUrl, string username, string password)
        {
            _logger = logger;
            _gitSourceControl = gitSourceControl;
            _bitbucketServerUrl = bitbucketServerUrl;
            _username = username;

            _httpClient = new HttpClient();

            var basicAuthHeaderValue = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}")));
            _httpClient.DefaultRequestHeaders.Authorization = basicAuthHeaderValue;
        }

        public List<RepositoryInfo> FetchRepositories(string[] repositoriesWhitelist = null)
        {
            _logger.Debug("Getting projects for username {Username}...", _username);

            var projects = GetProjects();

            _logger.Debug("Found {Count} projects...", projects.Count);

            var list = new List<RepositoryInfo>();
            foreach (var project in projects)
            {
                _logger.Debug("Getting repositories for project {Name}...", project.Name);

                var repos = GetRepositories(project.Key);

                _logger.Debug("Found {Count} repositories...", repos.Count);

                list.AddRange(repos);
            }

            if (repositoriesWhitelist != null)
                list = list.Where(r => repositoriesWhitelist.Any(x => x.Equals(r.Name, StringComparison.InvariantCultureIgnoreCase))).ToList();

            _logger.Information("Found {Count} repositories matching 1 of the following whitelists {RepositoriesWhitelist}...", list.Count, repositoriesWhitelist ?? new string[0]);

            return list;
        }

        public void EnsureRepositoriesSync(List<RepositoryInfo> repositories, string pathTemplate = DefaultRepoPathTemplate, string[] branchesWhitelist = null)
        {
            for (var index = 0; index < repositories.Count; index++)
            {
                var repo = repositories[index];

                _logger.Information("Ensuring sync [{Index}/{Count}] repository {Slug}...", index + 1, repositories.Count, repo.Slug);
                EnsureRepositorySync(repo, pathTemplate, branchesWhitelist);
            }
        }

        public void EnsureRepositorySync(RepositoryInfo repo, string pathTemplate = DefaultRepoPathTemplate, string[] branchesWhitelist = null)
        {
            if (string.IsNullOrEmpty(pathTemplate))
                pathTemplate = DefaultRepoPathTemplate;

            var relativeRepoPath = StringTemplate.Compile(pathTemplate, new Dictionary<string, string>
            {
                {"ProviderName", ProviderName},
                {"Namespace", repo.Namespace.ToLowerInvariant()},
                {"Slug", repo.Slug.ToLowerInvariant()}
            });

            var absoluteRepoPath = Path.GetFullPath(relativeRepoPath);

            try
            {
                CloneOrUpdateRepository(absoluteRepoPath, relativeRepoPath, branchesWhitelist, repo);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Something went wrong!");
            }
        }

        private void CloneOrUpdateRepository(string absoluteRepoPath,
            string relativeRepoPath, string[] branchesWhitelist, RepositoryInfo repo)
        {
            try
            {
                if (_gitSourceControl.IsRepository(absoluteRepoPath))
                {
                    _logger.Information("{Path} is already a repository. Attempting to update.", relativeRepoPath);
                    _gitSourceControl.UpdateRepository(absoluteRepoPath, branchesWhitelist);
                }
                else
                {
                    _gitSourceControl.CloneRepository(repo.HttpHref, absoluteRepoPath, branchesWhitelist);
                }
            }
            catch (LibGit2SharpException e)
            {
                // TODO: Fix possible infinite loop here
                if (e.Message.ToLowerInvariant().Contains("failed to send request"))
                {
                    _logger.Information("{ExMessage}... Retrying", e.Message);
                    CloneOrUpdateRepository(absoluteRepoPath, relativeRepoPath, branchesWhitelist, repo);
                }
            }
        }

        private List<RepositoryInfo> GetRepositories(string projectKey)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_bitbucketServerUrl}{RestApiSuffix}{ApiProjects}/{projectKey}{ApiRepositories}");

            using (var res = _httpClient.SendAsync(req).GetAwaiter().GetResult())
            using (var content = res.Content)
            {
                var data = content.ReadAsStringAsync().GetAwaiter().GetResult();

                return ((JArray)JsonConvert.DeserializeObject<dynamic>(data).values)
                    .Select(x => new RepositoryInfo(
                        name: (string)x["name"],
                        slug: (string)x["slug"],
                        namespaceName: projectKey,
                        httpHref: (string)x["links"]["clone"]
                            .Where(y => string.Equals((string)y["name"], "http"))
                            .Select(y => y["href"])
                            .First())
                    ).ToList();
            }
        }

        private List<BitbucketProjectInfo> GetProjects()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{_bitbucketServerUrl}{RestApiSuffix}{ApiProjects}");

            using (var res = _httpClient.SendAsync(req).GetAwaiter().GetResult())
            using (var content = res.Content)
            {
                var data = content.ReadAsStringAsync().GetAwaiter().GetResult();

                // TODO: fix page limit
                return ((JArray)JsonConvert.DeserializeObject<dynamic>(data).values)
                    .Select(x => new BitbucketProjectInfo(
                        key: (string)x["key"],
                        name: (string)x["name"],
                        href: (string)x["links"]["self"][0]["href"])
                    ).ToList();
            }
        }

    }

    public class BitbucketProjectInfo
    {
        public string Key { get; }
        public string Name { get; }
        public string Href { get; }

        public BitbucketProjectInfo(string key, string name, string href)
        {
            Key = key;
            Name = name;
            Href = href;
        }
    }
}