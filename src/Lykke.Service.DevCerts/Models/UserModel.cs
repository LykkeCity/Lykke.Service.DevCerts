using System;

namespace Lykke.Service.DevCerts.Models
{
    public class UserModel
    {
        public string RowKey { get; set; }
        public string Email { get; set; }
        public string CertPassword { get; set; }
        public string CertDate { get; set; }
        public string RevokeDate { get; set; }
        public bool HasCert { get; set; }
        public bool Admin { get; set; }
        public bool Visible { get; set; }
        public bool CertIsRevoked { get; set; }
    }
}
