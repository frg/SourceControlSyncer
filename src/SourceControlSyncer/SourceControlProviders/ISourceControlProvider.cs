using System.Collections.Generic;

namespace SourceControlSyncer.SourceControlProviders
{
    // Bitbucket
    // Github
    // Local?
    internal interface ISourceControlProvider
    {
        List<RepositoryInfo> FetchRepositories(string[] repositoriesWhitelist);
        void EnsureRepositoriesSync(List<RepositoryInfo> repositories, string pathTemplate, string[] branchesWhitelist);
        void EnsureRepositorySync(RepositoryInfo repo, string pathTemplate, string[] branchesWhitelist);
    }
}
