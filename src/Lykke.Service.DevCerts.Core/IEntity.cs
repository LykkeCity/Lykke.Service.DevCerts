namespace Lykke.Service.DevCerts.Core
{
    public interface IEntity
    {
        string RowKey { get; set; }

        string ETag { get; set; }
    }
}
