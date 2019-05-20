using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SourceControlSyncer.SourceControls
{
    internal interface ISourceControl
    {
        bool IsLocalRepository(string directory);
        SourceControlResult SyncRepository(RepositorySyncInfo repositorySyncInfo, string[] branchMatchers);
        SourceControlResult SyncRemoteRepository(RepositorySyncInfo repositorySyncInfo, string[] branchMatchers);
        SourceControlResult SyncLocalRepository(string repoDir, string[] branchMatchers);
    }

    internal interface ISourceControlAsync
    {
        Task<SourceControlResult[]> SyncRepositories(IEnumerable<RepositorySyncInfo> repositorySyncInfoList,
            string[] branchMatchers, CancellationToken cancellationToken);
    }

    public class RepositorySyncInfo
    {
        internal string RemoteUrl { get; set; }
        internal string LocalRepositoryDirectory { get; set; }
    }

    internal static class SourceControlResultFactory
    {
        public static SourceControlResult MakeSuccessful => new SourceControlResult { IsSuccessful = true };
        public static SourceControlResult MakeFailure(IEnumerable<SourceControlResultError> errors = null) => new SourceControlResult { IsSuccessful = false, Errors = errors };
        public static SourceControlResult MakeFailure(SourceControlResultError error = null) => new SourceControlResult { IsSuccessful = false, Errors = new List<SourceControlResultError> { error } };
        public static SourceControlResult MakeFailure(string message = null) => new SourceControlResult { IsSuccessful = false, Errors = new List<SourceControlResultError> { new SourceControlResultError{Message = message} } };
    }

    public class SourceControlResult
    {
        private IEnumerable<SourceControlResultError> _errors = new List<SourceControlResultError>();

        public bool IsSuccessful { get; set; }
        public IEnumerable<SourceControlResultError> Errors
        {
            get => _errors;
            set => _errors = value ?? new List<SourceControlResultError>();
        }
    }

    public class SourceControlResultError
    {
        public string Message { get; set; }
    }
}