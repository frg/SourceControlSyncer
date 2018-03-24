namespace SourceControlSyncer.SourceControlProviders
{
    public class RepositoryInfo
    {
        public string Name { get; }
        public string Slug { get; }
        public string Namespace { get; set; }
        public string HttpHref { get; }

        public RepositoryInfo(string name, string slug, string namespaceName, string httpHref)
        {
            Name = name;
            Slug = slug;
            Namespace = namespaceName;
            HttpHref = httpHref;
        }
    }
}
