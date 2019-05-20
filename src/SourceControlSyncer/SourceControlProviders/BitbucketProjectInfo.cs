namespace SourceControlSyncer.SourceControlProviders
{
    public class BitbucketProjectInfo
    {
        public BitbucketProjectInfo(string key, string name, string href)
        {
            Key = key;
            Name = name;
            Href = href;
        }

        public string Key { get; }
        public string Name { get; }
        public string Href { get; }
    }
}