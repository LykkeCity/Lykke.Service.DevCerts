using Lykke.Service.DevCerts.AzureRepositories.Extentions;
using Lykke.Service.DevCerts.Core.User;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Text;

namespace Lykke.Service.DevCerts.AzureRepositories.User
{
    public class UserActionHistoryEntity : TableEntity, IUserActionHistoryEntity
    {
        public string UserEmail { get; set; }
        public DateTime ActionDate { get; set; }
        public string IpAddress { get; set; }
        public string ControllerName { get; set; }
        public string ActionName { get; set; }
        public string Params { get; set; }

        public static string GeneratePartitionKey() => "UAH";

        public string GetRawKey() => ActionDate.StorageString();

        public static UserActionHistoryEntity Create(IUserActionHistoryEntity entity)
        {
            return new UserActionHistoryEntity
            {
                UserEmail = entity.UserEmail,
                ActionDate = entity.ActionDate,
                IpAddress = entity.IpAddress,
                ControllerName = entity.ControllerName,
                ActionName = entity.ActionName,
                Params = entity.Params,
                PartitionKey = GeneratePartitionKey(),
                RowKey = entity.ActionDate.StorageString()
            };
        }
    }
}
