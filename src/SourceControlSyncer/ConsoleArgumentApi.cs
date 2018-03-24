using System.Collections.Generic;
using PowerArgs;
using Serilog;

namespace SourceControlSyncer
{
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class ConsoleApi
    {
        private static readonly ILogger Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 838860800, rollOnFileSizeLimit: true)
            .CreateLogger();

        [HelpHook, ArgShortcut("-?"), ArgDescription("Shows this help")]
        public bool Help { get; set; }
        
        [ArgActionMethod, ArgDescription("Syncs your Bitbucker Server repositories")]
        public void BitbucketServer(BitbucketArgs args)
        {
            var userInfo = new UserInfo(args.Username, args.Email, args.Password);
            var sourceControl = new GitSourceControl(Logger, userInfo);
            var sourceControlProvider = new BitbucketProvider(Logger, sourceControl, args.Server, args.Username, args.Password);
            
            using (var stopwatch = new StopwatchHelper())
            {
                var repos = new List<RepositoryInfo>();
                using (var stopwatch2 = new StopwatchHelper())
                {
                    repos.AddRange(sourceControlProvider.FetchRepositories(args.RepositoryWhitelist));
                    Logger.Information("Fetching repositories took {TotalMs}ms ({Min}:{Sec} mm:ss)", stopwatch2.Result.TotalMilliseconds, stopwatch2.Result.Minutes, stopwatch2.Result.Seconds);
                }

                sourceControlProvider.EnsureRepositoriesSync(repos, args.RepositoryPathTemplate, args.BranchWhitelist);
                
                Logger.Information("Process took {TotalMs}ms ({Min}:{Sec} mm:ss)", stopwatch.Result.TotalMilliseconds, stopwatch.Result.Minutes, stopwatch.Result.Seconds);
            }
        }
        
        [ArgActionMethod, ArgDescription("Syncs your Github repositories")]
        public void Github(GithubArgs args)
        {
            var userInfo = new UserInfo(args.Username, args.Email, args.AccessToken);
            var sourceControl = new GitSourceControl(Logger, userInfo);
            var sourceControlProvider = new GithubProvider(Logger, sourceControl, args.Username, args.AccessToken);
            
            using (var stopwatch = new StopwatchHelper())
            {
                var repos = new List<RepositoryInfo>();
                using (var stopwatch2 = new StopwatchHelper())
                {
                    repos.AddRange(sourceControlProvider.FetchRepositories(args.RepositoryWhitelist));
                    Logger.Information("Fetching repositories took {TotalMs}ms ({Min}:{Sec} mm:ss)", stopwatch2.Result.TotalMilliseconds, stopwatch2.Result.Minutes, stopwatch2.Result.Seconds);
                }

                sourceControlProvider.EnsureRepositoriesSync(repos, args.RepositoryPathTemplate, args.BranchWhitelist);
                
                Logger.Information("Process took {TotalMs}ms ({Min}:{Sec} mm:ss)", stopwatch.Result.TotalMilliseconds, stopwatch.Result.Minutes, stopwatch.Result.Seconds);
            }
        }
    }

    public class GithubArgs : BaseArgs
    {
        [ArgRequired(PromptIfMissing=true), ArgDescription("The access token that will be used to authenticate with Github (scopes: \"repo\", \"read:org\")")]
        public string AccessToken { get; set; }

        [ArgRequired(PromptIfMissing=true), ArgDescription("The username that will be used to authenticate with Github")]
        public string Username { get; set; }

        [ArgRequired(PromptIfMissing=true), ArgDescription("The wmail that will be used with any source control signitures")]
        public string Email { get; set; }
    }

    public class BitbucketArgs : BaseArgs
    {
        [ArgRequired(PromptIfMissing=true), ArgDescription("The Bitbucket Server that will be queried for repositories")]
        public string Server { get; set; }

        [ArgRequired(PromptIfMissing=true), ArgDescription("The username that will be used to authenticate with the Bitbucket Server")]
        public string Username { get; set; }

        [ArgRequired(PromptIfMissing=true), ArgDescription("The wmail that will be used with any source control signitures")]
        public string Email { get; set; }

        [ArgRequired(PromptIfMissing=true), ArgDescription("The password that will be used to authenticate you with Bitbucket Server")]
        public string Password { get; set; }
    }

    public class BaseArgs
    {
        [ArgDescription("Enables / Disables logging to file")]
        public bool ShouldLog { get; set; } = true;
        
        [ArgDescription("Enables / Disables logging to the console (Errors will still be logged)")]
        public bool Silent { get; set; } = false;

        [ArgDescription("A template to identify where the repository will / is stored")]
        public string RepositoryPathTemplate { get; set; }
        
        [ArgDescription("Provides a way to whitelist which repositories to sync")]
        public string[] RepositoryWhitelist { get; set; } = null;
        
        [ArgDescription("Provides a way to blacklist repositories from syncing")]
        public string[] RepositoryBlacklist { get; set; } = null;

        [ArgDescription("Provides a way to whitelist which branches to sync (Default is \"develop\", \"master\", \"release\")")]
        public string[] BranchWhitelist { get; set; } = { "develop", "master", "release" };

        [ArgDescription("Provides a way to blacklist branches from syncing")]
        public string[] BrancheBlacklist { get; set; } = null;
    }
}
