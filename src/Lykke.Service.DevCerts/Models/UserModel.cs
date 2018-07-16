using System;

namespace Lykke.Service.DevCerts.Models
{
    public class UserModel
    {
        public string RowKey { get; set; }
        public string Email { get; set; }
        public string CertPassword { get; set; }
        public DateTime CertDate { get; set; }
        public DateTime RevokeDate { get; set; }
        public bool HasCert { get; set; }
        public bool Admin { get; set; }
        public bool CertIsRevoked { get; set; }
    }
}
