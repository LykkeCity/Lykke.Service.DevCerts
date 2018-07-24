using Autofac;
using AzureStorage.Tables;
using Lykke.Common.Log;
using Lykke.Service.DevCerts.AzureRepositories.Blob;
using Lykke.Service.DevCerts.AzureRepositories.User;
using Lykke.Service.DevCerts.Core.Blob;
using Lykke.Service.DevCerts.Core.User;
using Lykke.Service.DevCerts.Settings;
using Lykke.SettingsReader;

namespace Lykke.Service.DevCerts.Modules
{
    public class DbModule : Module
    {
        private readonly IReloadingManager<AppSettings> _appSettings;

        public DbModule(IReloadingManager<AppSettings> appSettings)
        {
            _appSettings = appSettings;

        }

        protected override void Load(ContainerBuilder builder)
        {
            var connectionString = _appSettings.ConnectionString(x => x.DevCertsService.Db.ConnectionString);
            var userConnectionString = _appSettings.ConnectionString(x => x.DevCertsService.Db.UserConnectionString);

            builder.Register(c =>
                new UserRepository(AzureTableStorage<UserEntity>.Create(userConnectionString,
                        "User",
                        c.Resolve<ILogFactory>())))
                        .As<IUserRepository>()
                        .SingleInstance();

            builder.Register(c =>
                new UserActionHistoryRepository(AzureTableStorage<UserActionHistoryEntity>.Create(userConnectionString,
                        "UserActionHistory",
                        c.Resolve<ILogFactory>()), new AzureBlobStorage(userConnectionString.CurrentValue), "useractionhistoryparam"))
                        .As<IUserActionHistoryRepository>()
                        .SingleInstance();


            builder.RegisterInstance(
                new BlobDataRepository(new AzureBlobStorage(connectionString.CurrentValue),"certificates", "certhistory")
            ).As<IBlobDataRepository>().SingleInstance();
        }
    }
}
