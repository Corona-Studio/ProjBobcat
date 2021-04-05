namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class AuthXSTSErrorModel
    {
        public string Identity { get; set; }
        public uint XErr { get; set; }
        public string Message { get; set; }
        public string Redirect { get; set; }
    }
}