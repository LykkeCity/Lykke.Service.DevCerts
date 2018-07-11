using System.Collections.Generic;
using System.Threading.Tasks;

namespace Lykke.Service.DevCerts.Core.User
{
    /// <summary>
    /// User repository
    /// </summary>
    public interface IUserRepository
    {
        /// <summary>
        /// Get user by email
        /// </summary>
        /// <param name="userEmail">email</param>
        /// <returns></returns>
        Task<IUserEntity> GetUserByUserEmail(string userEmail);
        /// <summary>
        /// Save user
        /// </summary>
        /// <param name="user">User</param>
        /// <returns></returns>
        Task<bool> SaveUser(IUserEntity user);
        /// <summary>
        /// Get list of all users
        /// </summary>
        /// <returns></returns>
        Task<List<IUserEntity>> GetUsers();
        /// <summary>
        /// REmove user by userEmail
        /// </summary>
        /// <param name="userEmail">userEmail</param>
        /// <returns></returns>
        Task<bool> RemoveUser(string userEmail);
    }
}
