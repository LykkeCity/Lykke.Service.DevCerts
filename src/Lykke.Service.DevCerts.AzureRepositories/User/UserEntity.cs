﻿using Lykke.Service.DevCerts.Core.User;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;

namespace Lykke.Service.DevCerts.AzureRepositories.User
{
    public class UserEntity : TableEntity, IUserEntity
    {
        public string Email { get; set ; }
        public string CertPassword { get; set; }
        public DateTime? CertDate { get; set; }
        public DateTime? RevokeDate { get; set; }
        public bool? HasCert { get; set; }
        public bool? Admin { get; set; }
        public bool? Visible { get; set; }
        public string CertMD5 { get; set; }
        public bool? CertIsRevoked { get; set; }

        public static string GeneratePartitionKey() => "U";

        public static string GenerateRowKey(string userEmail) => Guid.NewGuid().ToString();  

        public override void ReadEntity(IDictionary<string, EntityProperty> properties, OperationContext operationContext)
        {
            if (properties.TryGetValue("Email", out var email))
            {
                Email = email.StringValue;
            }

            if (properties.TryGetValue("CertPassword", out var certPassword))
            {
                CertPassword = certPassword.StringValue;
            }

            if (properties.TryGetValue("CertMD5", out var certMD5))
            {
                CertMD5 = certMD5.StringValue;
            }

            if (properties.TryGetValue("CertDate", out var certDate))
            {
                CertDate = certDate.DateTime;
            }

            if (properties.TryGetValue("RevokeDate", out var revokeDate))
            {
                RevokeDate = revokeDate.DateTime;
            }

            if (properties.TryGetValue("Visible", out var visible))
            {
                Visible = visible.BooleanValue;
            }

            if (properties.TryGetValue("HasCert", out var hasCert))
            {
                HasCert = hasCert.BooleanValue;
            }

            if (properties.TryGetValue("Admin", out var admin))
            {
                Admin = admin.BooleanValue;
            }

            if (properties.TryGetValue("CertIsRevoked", out var certIsRevoked))
            {
                CertIsRevoked = certIsRevoked.BooleanValue;
            }

        }

        public override IDictionary<string, EntityProperty> WriteEntity(OperationContext operationContext)
        {
            var dict = new Dictionary<string, EntityProperty>
            {
                {"Email", new EntityProperty(Email)},
                {"CertPassword", new EntityProperty(CertPassword)},
                {"CertDate", new EntityProperty(CertDate)},
                {"CertMD5", new EntityProperty(CertMD5)},
                {"RevokeDate", new EntityProperty(RevokeDate)},
                {"HasCert", new EntityProperty(HasCert)},
                {"Admin", new EntityProperty(Admin)},
                {"CertIsRevoked", new EntityProperty(CertIsRevoked)},
                {"Visible", new EntityProperty(Visible)}
            };

            return dict;
        }
    }
}
