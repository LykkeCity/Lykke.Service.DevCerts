using System;

namespace Lykke.Service.DevCerts.Core.User
{
    public interface IUserEntity : IEntity
    {
        string Email { get; set; }
        string CertMD5 { get; set; }
        string DevMD5 { get; set; }
        string TestMD5 { get; set; }
        bool? HasCert { get; set; }
        bool? Admin { get; set; }
        bool? Visible { get; set; }
        bool? DevAccess { get; set; }
        bool? TestAccess { get; set; }
        string CertPassword { get; set; }
        DateTime? CertDate { get; set; }
        DateTime? RevokeDate { get; set; }
        bool? CertIsRevoked { get; set; }
    }
}
