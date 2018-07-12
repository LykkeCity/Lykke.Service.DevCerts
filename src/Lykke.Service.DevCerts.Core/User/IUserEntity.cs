﻿using System;

namespace Lykke.Service.DevCerts.Core.User
{
    public interface IUserEntity : IEntity
    {
        string Email { get; set; }
        bool? HasCert { get; set; }
        bool? Admin { get; set; }
        string CertPassword { get; set; }
        DateTime? CertDate { get; set; }
        DateTime? RevokeDate { get; set; }
        bool? CertIsRevoked { get; set; }
    }
}