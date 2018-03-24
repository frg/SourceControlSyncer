using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using LibGit2Sharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;
using SourceControlSyncer.SourceControls;

namespace SourceControlSyncer.SourceControlProviders
{
    public class GithubProvider : ISourceControlProvider
    {
        private readonly ILogger _logger;
        private readonly GitSourceControl _gitSourceControl;
        private readonly string _username;
        private readonly string _accessToken;
        private readonly HttpClient _httpClient;
        
        private const string ProviderName = "github";
        private const string DefaultRepoPathTemplate = "./repos/{ProviderName}/{Namespace}/{Slug}";

        public GithubProvider(ILogger logger, GitSourceControl gitSourceControl, string username, string accessToken)
        {
            _logger = logger;
            _gitSourceControl = gitSourceControl;
            _username = username;
            _accessToken = accessToken;

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/65.0.3325.181 Safari/537.36");
        }

        public List<RepositoryInfo> FetchRepositories(string[] repositoriesWhitelist)
        {
            _logger.Debug("Getting repositories for username {Username}...", _username);

            var repos = GetRepositories();

            _logger.Debug("Found {Count} repositories...", repos.Count);

            if (repositoriesWhitelist != null)
                repos = repos.Where(r => repositoriesWhitelist.Any(x => x.Equals(r.Name, StringComparison.InvariantCultureIgnoreCase))).ToList();

            _logger.Information("Found {Count} repositories matching 1 of the following whitelists {RepositoriesWhitelist}...", repos.Count, repositoriesWhitelist ?? new string[0]);

            return repos;
        }

        public void EnsureRepositoriesSync(List<RepositoryInfo> repositories, string pathTemplate, string[] branchesWhitelist)
        {
            for (var index = 0; index < repositories.Count; index++)
            {
                var repo = repositories[index];

                _logger.Information("Ensuring sync [{Index}/{Count}] repository {Slug}...", index + 1, repositories.Count, repo.Slug);
                EnsureRepositorySync(repo, pathTemplate, branchesWhitelist);
            }
        }

        public void EnsureRepositorySync(RepositoryInfo repo, string pathTemplate, string[] branchesWhitelist)
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

        private List<RepositoryInfo> GetRepositories()
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/user/repos?access_token={_accessToken}");

            using (var res = _httpClient.SendAsync(req).GetAwaiter().GetResult())
            using (var content = res.Content)
            {
                var data = content.ReadAsStringAsync().GetAwaiter().GetResult();

                return ((JArray)JsonConvert.DeserializeObject<dynamic>(data))
                    .Select(x => new RepositoryInfo(
                        name: (string)x["name"],
                        slug: (string)x["name"],
                        namespaceName: (string)x["owner"]["login"],
                        httpHref: (string)x["clone_url"])
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
