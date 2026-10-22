
namespace NoName_Hotel.Application.Contracts.AuthContracts
{
    public interface IApiKeyValidatorService
    {
        Task<bool> IaValidAsync(string ApiKey, CancellationToken cancellationTokenv = default);
    }
}