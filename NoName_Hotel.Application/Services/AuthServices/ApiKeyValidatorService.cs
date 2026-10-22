using Microsoft.EntityFrameworkCore;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Domain.Data;

namespace NoName_Hotel.Application.Services.AuthServices
{
    //if you are using a db table, you would inject the dbcontext inside
    //the service and you would query the apikeys table for the value,
    //and if there is expiration, you would make sure it hasnt expired


    public class ApiKeyValidatorService(NoName_hotel_DbContext dbContext) : IApiKeyValidatorService
    //public class ApiKeyValidatorService(IConfiguration configuration) : IApiKeyValidatorService
    {
        public async Task<bool> IaValidAsync(string ApiKey, CancellationToken cancellationTokenv = default)
        {
            if (string.IsNullOrWhiteSpace(ApiKey)) return false;

            var apiKeyEntity = await dbContext.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k  => k.Key == ApiKey, cancellationTokenv);

            if(apiKeyEntity == null) return false;

            return apiKeyEntity.IsActive;
        }
    }
}
