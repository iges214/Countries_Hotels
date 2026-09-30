
namespace NoName_Hotel.api.Contracts.AuthContracts
{
    public interface IApiKeyValidatorService
    {
        Task<bool> IaValidAsync(string ApiKey, CancellationToken cancellationToken);
    }
}