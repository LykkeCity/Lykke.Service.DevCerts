using Lykke.SettingsReader.Attributes;

namespace Lykke.Service.DevCerts.Settings
{
    public class DbSettings
    {
        [AzureTableCheck]
        public string LogsConnString { get; set; }

        [AzureTableCheck]
        public string UserConnectionString { get; set; }

        [AzureTableCheck]
        public string ConnectionString { get; set; }
    }
}
