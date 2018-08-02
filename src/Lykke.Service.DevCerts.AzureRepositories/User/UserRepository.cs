using AzureStorage;
using Lykke.Service.DevCerts.Core.User;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.AzureRepositories.User
{
    public class UserRepository : IUserRepository
    {
        private readonly INoSQLTableStorage<UserEntity> _tableStorage;

        public UserRepository(INoSQLTableStorage<UserEntity> tableStorage)
        {
            _tableStorage = tableStorage;
        }

        public async Task<IUserEntity> GetUserByUserEmail(string userEmail)
        {
            var Users = await GetUsers();
            string creds = "";
            if (userEmail.Contains('@'))
                creds = userEmail.Substring(0, userEmail.IndexOf('@'));
            else
                creds = userEmail;
            return Users.Where(u => u.Email.Contains(creds)).OrderByDescending(u => u.CertDate).FirstOrDefault();
        }

        public async Task<IUserEntity> GetUserByRowKey(string RowKey)
        {
            return await _tableStorage.GetDataAsync(UserEntity.GeneratePartitionKey(), RowKey);
        }

        public async Task<bool> SaveUser(IUserEntity user)
        {
            try
            {
                var userEntity = await GetUserByUserEmail(user.Email);
                var te = (UserEntity)user;
                if (userEntity == null)
                {
                    te.RowKey = UserEntity.GenerateRowKey(te.Email);
                }
                else
                {
                    te.Email = user.Email;
                    te.RowKey = userEntity.RowKey;
                }

                if (te.PartitionKey == null)
                {
                    te.PartitionKey = UserEntity.GeneratePartitionKey();
                }
                await _tableStorage.InsertOrMergeAsync(te);
            }


            catch(Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        public async Task<List<IUserEntity>> GetUsers()
        {
            var pk = UserEntity.GeneratePartitionKey();
            return (await _tableStorage.GetDataAsync(pk)).Cast<IUserEntity>().ToList();
        }

        public async Task<bool> RemoveUser(string userEmail)
        {
            try
            {
                await _tableStorage.DeleteAsync(UserEntity.GeneratePartitionKey(), UserEntity.GenerateRowKey(userEmail));
            }
            catch
            {
                return false;
            }

            return true;
        }
    }
}
