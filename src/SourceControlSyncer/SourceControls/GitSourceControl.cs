using System;
using System.Linq;
using LibGit2Sharp;
using Serilog;

namespace SourceControlSyncer.SourceControls
{
    public class GitSourceControl : ISourceControl
    {
        private readonly ILogger _logger;
        private readonly UserInfo _userInfo;
        private readonly UsernamePasswordCredentials _credentials;

        public GitSourceControl(ILogger logger, UserInfo userInfo)
        {
            _logger = logger;
            _userInfo = userInfo;

            _credentials = new UsernamePasswordCredentials { Username = _userInfo.Username, Password = _userInfo.Password };
        }

        public bool IsRepository(string direcctory)
        {
            try
            {
                // ReSharper disable once ObjectCreationAsStatement
                new Repository(direcctory);
            }
            catch (RepositoryNotFoundException)
            {
                return false;
            }

            return true;
        }

        public void CloneRepository(string repoUrl, string cloneToDir, string[] reposWhitelist = null)
        {
            Repository.Clone(repoUrl, cloneToDir, new CloneOptions
            {
                CredentialsProvider = (url, user, cred) => _credentials
            });

            CheckoutRepositoryRemotes(cloneToDir, reposWhitelist);
        }

        public void UpdateRepository(string repoDir, string[] reposWhitelist = null)
        {
            var signiture = new Signature(_userInfo.Username, _userInfo.Email, new DateTimeOffset(DateTime.Now));

            CheckoutRepositoryRemotes(repoDir, reposWhitelist);

            using (var repo = new Repository(repoDir))
            {
                foreach (var branch in repo.Branches.Where(b => !b.IsRemote))
                {
                    Commands.Checkout(repo, branch);

                    var options = new PullOptions
                    {
                        FetchOptions = new FetchOptions
                        {
                            CredentialsProvider = (url, usernameFromUrl, types) => _credentials
                        },
                        MergeOptions = new MergeOptions
                        {
                            FileConflictStrategy = CheckoutFileConflictStrategy.Theirs
                        }
                    };

                    Commands.Pull(repo, signiture, options);
                }
            }
        }

        private void CheckoutRepositoryRemotes(string repoDir, string[] brachesWhitelist)
        {
            using (var repo = new Repository(repoDir))
            {
                var remoteBranches = repo.Branches
                    .Where(b => b.IsRemote)
                    .ToList();

                if (brachesWhitelist != null)
                    remoteBranches = remoteBranches
                        .Where(b => brachesWhitelist
                            .Any(c => b.CanonicalName.Trim().ToLowerInvariant().StartsWith($"refs/remotes/origin/{c}")))
                        .ToList();

                _logger.Information("Found {Count} branches starting with 1 of the following whitelists {BrachesWhitelist}...", remoteBranches.Count, brachesWhitelist ?? new string[0]);

                for (var index = 0; index < remoteBranches.Count; index++)
                {
                    var branch = remoteBranches[index];
                    _logger.Information("Checking out [{Index}/{Count}] {BranchName}...", index + 1, remoteBranches.Count, branch.FriendlyName);

                    Commands.Checkout(repo, branch);
                }
            }
        }
    }
}