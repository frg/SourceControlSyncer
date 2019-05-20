using System.Collections.Generic;
using System.Threading.Tasks;

namespace SourceControlSyncer.SourceControlProviders
{
    // Bitbucket
    // Github
    // Local?
    internal interface ISourceControlProvider
    {
        Task<List<RepositoryInfo>> FetchRepositories(string[] reposMatchers);
        Task EnsureRepositoriesSync(List<RepositoryInfo> repositories, string pathTemplate, string[] branchMatchers);
    }
}