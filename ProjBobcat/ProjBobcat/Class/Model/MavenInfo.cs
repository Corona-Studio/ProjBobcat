namespace ProjBobcat.Class.Model
{
    /// <summary>
    ///     Maven包的有关信息。
    ///     Maven Package Information.
    /// </summary>
    public class MavenInfo
    {
        /// <summary>
        ///     组织名。
        /// </summary>
        public string OrganizationName { get; set; }

        /// <summary>
        ///     项目名。
        /// </summary>
        public string ArtifactId { get; set; }

        /// <summary>
        ///     版本号。
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        ///     分类串。
        /// </summary>
        public string Classifier { get; set; }

        /// <summary>
        ///     类型。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        ///     是否为快照。
        /// </summary>
        public bool IsSnapshot { get; set; }

        /// <summary>
        ///     路径。
        /// </summary>
        public string Path { get; set; }
    }
}