using Lykke.Service.DevCerts.Core.User;
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
        public string CertMD5 { get; set; }
        public string DevMD5 { get; set; }
        public string TestMD5 { get; set; }
        public DateTime? CertDate { get; set; }
        public DateTime? RevokeDate { get; set; }
        public bool? HasCert { get; set; }
        public bool? Admin { get; set; }
        public bool? Visible { get; set; }
        public bool? DevAccess { get; set; }
        public bool? TestAccess { get; set; }
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

            if (properties.TryGetValue("DevMD5", out var devMD5))
            {
                DevMD5 = devMD5.StringValue;
            }

            if (properties.TryGetValue("TestMD5", out var testMD5))
            {
                TestMD5 = testMD5.StringValue;
            }

            if (properties.TryGetValue("CertDate", out var certDate))
            {
                CertDate = certDate.DateTime;
            }

            if (properties.TryGetValue("RevokeDate", out var revokeDate))
            {
                RevokeDate = revokeDate.DateTime;
            }

            if (properties.TryGetValue("HasCert", out var hasCert))
            {
                HasCert = hasCert.BooleanValue;
            }

            if (properties.TryGetValue("Admin", out var admin))
            {
                Admin = admin.BooleanValue;
            }

            if (properties.TryGetValue("Visible", out var visible))
            {
                Visible = visible.BooleanValue;
            }

            if (properties.TryGetValue("DevAccess", out var devAccess))
            {
                DevAccess = devAccess.BooleanValue;
            }

            if (properties.TryGetValue("TestAccess", out var testAccess))
            {
                TestAccess = testAccess.BooleanValue;
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
                {"RevokeDate", new EntityProperty(RevokeDate)},
                {"CertMD5", new EntityProperty(CertMD5)},
                {"DevMD5", new EntityProperty(DevMD5)},
                {"TestMD5", new EntityProperty(TestMD5)},
                {"HasCert", new EntityProperty(HasCert)},
                {"Admin", new EntityProperty(Admin)},
                {"DevAccess", new EntityProperty(DevAccess)},
                {"TestAccess", new EntityProperty(TestAccess)},
                {"Visible", new EntityProperty(Visible)},
                {"CertIsRevoked", new EntityProperty(CertIsRevoked)},
            };

            return dict;
        }
    }
}
