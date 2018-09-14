
using AzureStorage;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.AzureRepositories.User
{
    public class UserActionHistoryRepository : IUserActionHistoryRepository
    {
        private readonly INoSQLTableStorage<UserActionHistoryEntity> _tableStorage;
        private readonly Core.Blob.IBlobStorage _blobStorage;
        private readonly string _container;

        public UserActionHistoryRepository(INoSQLTableStorage<UserActionHistoryEntity> tableStorage, Core.Blob.IBlobStorage blobStorage, string container)
        {
            _tableStorage = tableStorage;
            _blobStorage = blobStorage;
            _container = container;
        }

        public async Task SaveUserActionHistoryAsync(IUserActionHistoryEntity userActionHistory)
        {
            var entity = UserActionHistoryEntity.Create(userActionHistory);
            if (!string.IsNullOrEmpty(entity.Params))
            {
                var parms = entity.Params;
                entity.Params = Guid.NewGuid().ToString();

                if (!string.IsNullOrEmpty(parms))
                {
                    var data = Encoding.UTF8.GetBytes(parms);
                    await _blobStorage.SaveBlobAsync(_container, entity.Params, data);
                }
            }

            await _tableStorage.InsertOrMergeAsync(entity);
        }
    }
}
