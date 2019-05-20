using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace SourceControlSyncer.SourceControls
{
    public class GitSourceControlAsync : GitSourceControl, ISourceControlAsync
    {
        private readonly ILogger _logger;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(10, 10);

        public GitSourceControlAsync(ILogger logger, UserInfo userInfo) : base(logger, userInfo)
        {
            _logger = logger;
        }

        public async Task<SourceControlResult[]> SyncRepositories(IEnumerable<RepositorySyncInfo> repositorySyncInfoList,
            string[] branchMatchers,
            CancellationToken cancellationToken)
        {
            var tasks = new ConcurrentBag<Task<SourceControlResult>>();
            var repositorySyncInfos = repositorySyncInfoList.ToList();
            for (var i = 0; i < repositorySyncInfos.Count; i++)
            {
                var repositorySyncInfo = repositorySyncInfos[i];

                await _semaphore.WaitAsync(cancellationToken);

                var index = i + 1;
                var task = Task.Run(() =>
                {
                    try
                    {
                        _logger.Information("Ensuring sync [{Index}/{Count}] repository {RemoteUrl}...", index,
                        repositorySyncInfos.Count, repositorySyncInfo.RemoteUrl);
                        return SyncRepository(repositorySyncInfo, branchMatchers);
                    }
                    finally
                    {
                        // Once we're ready from syncing a repo, release a lock
                        _semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            return await Task.WhenAll(tasks);
        }
    }
}
