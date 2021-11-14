using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Model.MicrosoftAuth
{
    public class MicrosoftAuthenticatorAPISettings
    {
        public string ClientId { get; set; }
        public string TenentId { get; set; }
        public string[] Scopes { get; set; }

    }
}
