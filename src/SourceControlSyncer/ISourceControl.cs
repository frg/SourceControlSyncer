namespace SourceControlSyncer
{
    interface ISourceControl
    {
        bool IsRepository(string direcctory);
        void CloneRepository(string repoUrl, string cloneToDir, string[] branches);
        void UpdateRepository(string repoDir, string[] branches);
    }
}
