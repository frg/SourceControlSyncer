using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using LibGit2Sharp;
using LibGit2Sharp.Handlers;
using PowerArgs;
using Serilog;

namespace SourceControlSyncer.SourceControls
{
    public class GitSourceControl : ISourceControl
    {
        private readonly UsernamePasswordCredentials _credentials;
        private readonly ILogger _logger;
        private readonly UserInfo _userInfo;

        public GitSourceControl(ILogger logger, UserInfo userInfo)
        {
            _logger = logger;
            _userInfo = userInfo;

            _credentials = new UsernamePasswordCredentials
            { Username = _userInfo.Username, Password = _userInfo.Password };
        }

        public bool IsLocalRepository(string directory)
        {
            try
            {
                // ReSharper disable once ObjectCreationAsStatement
                new Repository(directory);
            }
            catch (RepositoryNotFoundException)
            {
                return false;
            }

            return true;
        }

        public SourceControlResult SyncRepository(RepositorySyncInfo repositorySyncInfo, string[] branchMatchers)
        {
            try
            {
                if (IsLocalRepository(repositorySyncInfo.LocalRepositoryDirectory))
                {
                    _logger.Information("{Path} is already a repository. Attempting to update.", repositorySyncInfo.LocalRepositoryDirectory);
                    return SyncLocalRepository(repositorySyncInfo.LocalRepositoryDirectory, branchMatchers);
                }

                return SyncRemoteRepository(repositorySyncInfo, branchMatchers);
            }
            catch (LibGit2SharpException ex)
            {
                // TODO: Fix possible infinite loop here
                if (ex.Message.ToLowerInvariant().Contains("failed to send request"))
                {
                    _logger.Information("{ExMessage}... Retrying", ex.Message);
                    return SyncRepository(repositorySyncInfo, branchMatchers);
                }

                if (ex.Message.ToLowerInvariant().Contains("unsupported url protocol"))
                {
                    _logger.Information("{ExMessage}... Skipping", ex.Message);
                    return SourceControlResultFactory.MakeFailure(ex.Message);
                }

                throw;
            }
        }

        public SourceControlResult SyncRemoteRepository(RepositorySyncInfo repositorySyncInfo, string[] branchMatchers = null)
        {
            branchMatchers = branchMatchers ?? new string[0];
            var transferLastUpdate = DateTime.UtcNow;

            bool OnTransferProgress(TransferProgress progress)
            {
                // Log an update every x seconds
                if (DateTime.UtcNow > transferLastUpdate.AddSeconds(5))
                {
                    transferLastUpdate = DateTime.UtcNow;
                    _logger.Debug(
                        $"Transfer progress {100 * ((double)progress.ReceivedObjects / progress.TotalObjects):0.##}%, Objects: {progress.ReceivedObjects} of {progress.TotalObjects}, Bytes: {progress.ReceivedBytes}");
                }

                return true;
            }

            var checkoutLastUpdate = DateTime.UtcNow;

            void OnCheckoutProgress(string path, int completedSteps, int totalSteps)
            {
                // Log an update every x seconds
                if (DateTime.UtcNow > checkoutLastUpdate.AddSeconds(5))
                {
                    checkoutLastUpdate = DateTime.UtcNow;
                    _logger.Debug($"Checkout progress {100 * ((double)completedSteps / totalSteps):0.##}%");
                }
            }

            try
            {
                Repository.Clone(repositorySyncInfo.RemoteUrl, repositorySyncInfo.LocalRepositoryDirectory, new CloneOptions
                {
                    CredentialsProvider = (url, user, cred) => _credentials,
                    OnTransferProgress = OnTransferProgress,
                    OnCheckoutProgress = OnCheckoutProgress
                });
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clone repository. Continuing");

                return SourceControlResultFactory.MakeFailure(ex.Message);
            }

            return SyncLocalRepository(repositorySyncInfo.LocalRepositoryDirectory, branchMatchers);
        }

        public SourceControlResult SyncLocalRepository(string repoDir, string[] branchMatchers)
        {
            branchMatchers = branchMatchers ?? new string[0];

            using (var repo = new Repository(repoDir))
            {
                FetchFromAllRemotes(repo);
                
                var branchesToProcess = FilterBranchesByMatchers(repo.Branches.ToList(), branchMatchers);
                var branchesToProcessInitialCount = branchesToProcess.Count;
                var branchesToProcessCurrentCount = 0;

                _logger.Information(
                    "Found {Count} branch(es) starting with at least of the following matchers {BranchMatchers}...",
                    branchesToProcess.Count, branchMatchers);

                // Prepare for pull
                var signature = new Signature(_userInfo.Username, _userInfo.Email, DateTimeOffset.Now);
                var pullOptions = new PullOptions
                {
                    FetchOptions = new FetchOptions
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) => _credentials
                    },
                    MergeOptions = new MergeOptions
                    {
                        FastForwardStrategy = FastForwardStrategy.Default
                    }
                };


                var localTrackingBranches = branchesToProcess
                    .Where(branch => !branch.IsRemote)
                    .Where(branch => !branch.FriendlyName.EndsWith("/HEAD"))
                    .Where(branch => branch.IsTracking)
                    .ToList();

                var remoteBranches = branchesToProcess
                    .Where(branch => branch.IsRemote)
                    .Where(branch => !branch.FriendlyName.EndsWith("/HEAD"))
                    // Where local branch tracking this remote does not exist
                    .Where(branch => !localTrackingBranches.Any(x => x.TrackedBranch.FriendlyName.Equals(branch.FriendlyName)))
                    .ToList();

                // Checkout and pull branches that do not have local branches that track remotes
                branchesToProcess = branchesToProcess.Except(remoteBranches).ToList();
                foreach (var branch in remoteBranches)
                {
                    branchesToProcessCurrentCount++;
                    var localBranchName = branch.UpstreamBranchCanonicalName.Substring("refs/heads/".Length, branch.UpstreamBranchCanonicalName.Length - "refs/heads/".Length);
                    
                    _logger.Information("Branch {BranchName} does not exist locally. Creating", localBranchName);
                    var localBranch = repo.CreateBranch(localBranchName, branch.Tip);
                    repo.Branches.Update(localBranch, b => b.TrackedBranch = branch.CanonicalName);

                    _logger.Information("Checking out [{Index}/{Count}] {BranchName}...", branchesToProcessCurrentCount,
                        branchesToProcessInitialCount, localBranch.FriendlyName);
                    Commands.Checkout(repo, localBranch, new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force });
                }

                // Pull local branches that track remotes
                branchesToProcess = branchesToProcess.Except(localTrackingBranches).ToList();
                foreach (var branch in localTrackingBranches)
                {
                    _logger.Information("Checking out [{Index}/{Count}] {BranchName}...", branchesToProcessCurrentCount, branchesToProcessInitialCount, branch.FriendlyName);
                    var checkoutOptions = new CheckoutOptions { CheckoutModifiers = CheckoutModifiers.Force };
                    Commands.Checkout(repo, branch, checkoutOptions);

                    _logger.Information("Pulling {BranchName}...", branch.FriendlyName);
                    Commands.Pull(repo, signature, pullOptions);
                }

                if (branchesToProcess.Any())
                {
                    _logger.Warning("Found {Count} branches that weren't processed. {Branches}", branchesToProcess.Count, string.Join(",", branchesToProcess.Select(x => x.FriendlyName)));
                }
            }

            return SourceControlResultFactory.MakeSuccessful;
        }

        private void FetchFromAllRemotes(Repository repo)
        {
            string fetchLogMessage = null;
            var options = new FetchOptions
            {
                CredentialsProvider = (url, usernameFromUrl, types) => _credentials
            };

            foreach (var remote in repo.Network.Remotes)
            {
                var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);
                Commands.Fetch(repo, remote.Name, refSpecs, options, fetchLogMessage);
            }
        }

        private static List<Branch> FilterBranchesByRemotesOnly(IEnumerable<Branch> branches)
        {
            return branches
                .Where(branch => branch.IsRemote)
                .Where(branch => !branch.FriendlyName.EndsWith("/HEAD"))
                .ToList();
        }

        private Func<Branch, bool> LocalBranchNotExistsAndRemoteBranchesFirst(IRepository repo)
        {
            // Pre compute local branch name for performance
            var localBranchNames = new Dictionary<Branch, string>();
            repo.Branches.ForEach(branch =>
            {
                var localBranchName = ToLocalBranchName(branch.FriendlyName, branch.RemoteName);
                localBranchNames.Add(branch, localBranchName);
            });

            // Pre compute is remote and does local branch exist value for performance
            var isRemoteBranches = new Dictionary<Branch, bool>();
            repo.Branches.ForEach(branch =>
            {
                var exists = localBranchNames.TryGetValue(branch, out var localBranchName);
                if (!exists) _logger.Error("Pre computed local branch name for branch {BranchName} was not found!", branch.FriendlyName);
                isRemoteBranches.Add(branch, !branch.IsRemote && branch.FriendlyName.Equals(localBranchName, StringComparison.OrdinalIgnoreCase));
            });

            return branch =>
            {
                var exists = isRemoteBranches.TryGetValue(branch, out var isRemote);
                if (!exists) _logger.Error("Pre computed is remote branch for branch {BranchName} was not found!", branch.FriendlyName);
                return isRemote;
            };
        }

        private List<Branch> FilterBranchesByMatchers(List<Branch> remoteBranches, string[] branchMatchers)
        {
            if (branchMatchers != null && branchMatchers.Any())
            {
                // Pre compute local branch name for performance
                var localBranchNames = new Dictionary<Branch, string>();
                remoteBranches.ForEach(branch =>
                {
                    var localBranchName = ToLocalBranchName(branch.FriendlyName, branch.RemoteName);
                    localBranchNames.Add(branch, localBranchName);
                });

                var matchers = branchMatchers.Select(x =>
                    new Regex(x, RegexOptions.Compiled | RegexOptions.IgnoreCase));

                remoteBranches = remoteBranches
                    .Where(branch => matchers
                        .Any(regex =>
                        {
                            var exists = localBranchNames.TryGetValue(branch, out var localBranchName);
                            if (!exists) _logger.Error("Pre computed local branch name for branch {BranchName} was not found!", branch.FriendlyName);
                            return regex.Matches(localBranchName).Count > 0;
                        }))
                    .ToList();
            }

            return remoteBranches;
        }

        private static string ToLocalBranchName(string branchName, string remoteName)
        {
            var remoteSubString = $"{remoteName}/";
            if (branchName.StartsWith(remoteSubString))
            {
                return branchName.Substring(remoteSubString.Length, branchName.Length - remoteSubString.Length);
            }

            return branchName;
        }
    }
}