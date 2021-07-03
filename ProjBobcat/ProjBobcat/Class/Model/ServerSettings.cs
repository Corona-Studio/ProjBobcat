namespace ProjBobcat.Class.Model
{
    public class ServerSettings
    {
        public string Address { get; set; }
        public ushort Port { get; set; }
        public string DisplayStr => ToString();
        public bool IsDefault => string.IsNullOrEmpty(Address) || Port == 0;

        public override string ToString()
        {
            if (IsDefault) return "未指定服务器。";
            return $"{Address}:{Port}";
        }
    }
}