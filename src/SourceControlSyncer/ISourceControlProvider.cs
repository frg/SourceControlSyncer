using System.Collections.Generic;

namespace SourceControlSyncer
{
    // Bitbucket
    // Github
    // Local?
    interface ISourceControlProvider
    {
        List<RepositoryInfo> FetchRepositories(string[] repositoriesWhitelist);
        void EnsureRepositoriesSync(List<RepositoryInfo> repositories, string pathTemplate, string[] branchesWhitelist);
        void EnsureRepositorySync(RepositoryInfo repo, string pathTemplate, string[] branchesWhitelist);
    }
}
