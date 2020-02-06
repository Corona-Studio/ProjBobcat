namespace ProjBobcat.Class.Model
{
    public class MavenInfo
    {
        public string OrganizationName { get; set; }
        public string ArtifactId { get; set; }
        public string Version { get; set; }
        public string Classifier { get; set; }
        public string Type { get; set; }
        public bool IsSnapshot { get; set; }
        public string Path { get; set; }
    }
}