using System;
using System.Collections.Generic;
using PowerArgs;
using Serilog;
using SourceControlSyncer.SourceControlProviders;
using SourceControlSyncer.SourceControls;

namespace SourceControlSyncer
{
    [ArgExceptionBehavior(ArgExceptionPolicy.StandardExceptionHandling)]
    public class ConsoleApi
    {
        private static readonly ILogger Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File("Logs/log.txt", rollingInterval: RollingInterval.Day, fileSizeLimitBytes: 838860800,
                rollOnFileSizeLimit: true)
            .CreateLogger();

        [HelpHook]
        [ArgShortcut("-?")]
        [ArgDescription("Shows the help")]
        public bool Help { get; set; }

        [ArgActionMethod]
        [ArgDescription("Syncs your Bitbucket Server repositories")]
        public void BitbucketServer(BitbucketServerArgs args)
        {
            try
            {
                var userInfo = new UserInfo(args.Username, args.Email, args.Password);
                var sourceControl = new GitSourceControlAsync(Logger, userInfo);
                var sourceControlProvider = new BitbucketServerProvider(Logger, sourceControl, args.ServerUrl,
                    args.Username, args.Password);

                using (var stopwatch = new StopwatchHelper())
                {
                    var repos = new List<RepositoryInfo>();
                    using (var stopwatch2 = new StopwatchHelper())
                    {
                        repos.AddRange(sourceControlProvider.FetchRepositories(args.RepositoryMatchers)
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult());
                        Logger.Information("Fetching repositories took {TotalMs}ms ({Min}:{Sec} mm:ss)",
                            stopwatch2.Result.TotalMilliseconds, stopwatch2.Result.Minutes, stopwatch2.Result.Seconds);
                    }

                    sourceControlProvider.EnsureRepositoriesSync(
                            repos,
                            args.RepositoryPathTemplate,
                            args.BranchMatchers)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                    Logger.Information("Done! Process took {TotalMs}ms ({Min}:{Sec} mm:ss)",
                        stopwatch.Result.TotalMilliseconds, stopwatch.Result.Minutes, stopwatch.Result.Seconds);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "There was an unhandled exception!");
                Console.ReadLine();
            }
        }

        [ArgActionMethod]
        [ArgDescription("Syncs your Bitbucket Cloud repositories")]
        public void BitbucketCloud(BitbucketCloudArgs args)
        {
            try
            {
                var userInfo = new UserInfo(args.Username, args.Email, args.Password);
                var sourceControl = new GitSourceControlAsync(Logger, userInfo);
                var sourceControlProvider = new BitbucketCloudProvider(Logger, sourceControl, args.AccountUsername,
                    args.Username, args.Password);

                using (var stopwatch = new StopwatchHelper())
                {
                    var repos = new List<RepositoryInfo>();
                    using (var stopwatch2 = new StopwatchHelper())
                    {
                        repos.AddRange(sourceControlProvider.FetchRepositories(args.RepositoryMatchers)
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult());
                        Logger.Information("Fetching repositories took {TotalMs}ms ({Min}:{Sec} mm:ss)",
                            stopwatch2.Result.TotalMilliseconds, stopwatch2.Result.Minutes, stopwatch2.Result.Seconds);
                    }

                    sourceControlProvider.EnsureRepositoriesSync(
                            repos,
                            args.RepositoryPathTemplate,
                            args.BranchMatchers)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                    Logger.Information("Done! Process took {TotalMs}ms ({Min}:{Sec} mm:ss)",
                        stopwatch.Result.TotalMilliseconds, stopwatch.Result.Minutes, stopwatch.Result.Seconds);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "There was an unhandled exception!");
                Console.ReadLine();
            }
        }

        [ArgActionMethod]
        [ArgDescription("Syncs your Github repositories")]
        public void Github(GithubArgs args)
        {
            try
            {
                var userInfo = new UserInfo(args.Username, args.Email, args.AccessToken);
                var sourceControl = new GitSourceControlAsync(Logger, userInfo);
                var sourceControlProvider = new GithubProvider(Logger, sourceControl, args.Username, args.AccessToken);

                using (var stopwatch = new StopwatchHelper())
                {
                    var repos = new List<RepositoryInfo>();
                    using (var stopwatch2 = new StopwatchHelper())
                    {
                        repos.AddRange(sourceControlProvider.FetchRepositories(args.RepositoryMatchers)
                            .ConfigureAwait(false)
                            .GetAwaiter()
                            .GetResult());

                        Logger.Information("Fetching repositories took {TotalMs}ms ({Min}:{Sec} mm:ss)",
                            stopwatch2.Result.TotalMilliseconds, stopwatch2.Result.Minutes, stopwatch2.Result.Seconds);
                    }

                    sourceControlProvider.EnsureRepositoriesSync(repos,
                            args.RepositoryPathTemplate,
                            args.BranchMatchers)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();

                    Logger.Information("Done! Process took {TotalMs}ms ({Min}:{Sec} mm:ss)",
                        stopwatch.Result.TotalMilliseconds, stopwatch.Result.Minutes, stopwatch.Result.Seconds);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "There was an unhandled exception!");
                Console.ReadLine();
            }
        }
    }

    public class GithubArgs : BaseArgs
    {
        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-at")]
        [ArgDescription(
            "The access token that will be used to authenticate with Github (scopes: \"repo\", \"read:org\")")]
        public string AccessToken { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-u")]
        [ArgDescription("The username that will be used to authenticate & interact with Github")]
        public string Username { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-e")]
        [ArgDescription("The email that will be used with any source control signitures")]
        public string Email { get; set; }
    }

    public class BitbucketServerArgs : BaseArgs
    {
        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-url")]
        [ArgDescription("The Bitbucket Server that will be queried for repositories")]
        public string ServerUrl { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-u")]
        [ArgDescription("The username that will be used to authenticate with the Bitbucket Server")]
        public string Username { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-e")]
        [ArgDescription("The email that will be used with any source control signatures")]
        public string Email { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-p")]
        [ArgDescription("The password that will be used to authenticate you with Bitbucket Server")]
        public string Password { get; set; }
    }

    public class BitbucketCloudArgs : BaseArgs
    {
        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-au")]
        [ArgDescription("The Bitbucket Cloud account that will be queried for repositories")]
        public string AccountUsername { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-u")]
        [ArgDescription("The username that will be used to authenticate with the Bitbucket Server")]
        public string Username { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-e")]
        [ArgDescription("The email that will be used with any source control signatures")]
        public string Email { get; set; }

        [ArgRequired(PromptIfMissing = true)]
        [ArgShortcut("-p")]
        [ArgDescription("The password that will be used to authenticate you with Bitbucket Server")]
        public string Password { get; set; }
    }

    public class BaseArgs
    {
        [ArgDescription("Enables / disables logging to file")]
        public bool ShouldLog { get; set; } = true;

        [ArgDescription("Enables / Disables logging to the console (Errors will still be logged)")]
        public bool Silent { get; set; } = false;

        [ArgDescription("A template to identify where the repository will be / is stored")]
        [ArgShortcut("-dir")]
        public string RepositoryPathTemplate { get; set; }

        [ArgDescription("Provides a way to match (using regex) which repositories to sync")]
        [ArgShortcut("-rw")]
        public string[] RepositoryMatchers { get; set; } = null;

        [ArgDescription("Provides a way to match (using regex) which branches to sync")]
        [ArgShortcut("-bw")]
        public string[] BranchMatchers { get; set; } = null;
    }
}