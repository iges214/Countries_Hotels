using Microsoft.EntityFrameworkCore;
using NoName_Hotel.api.Contracts.AuthContracts;
using NoName_Hotel.api.Data;

namespace NoName_Hotel.api.Services.AuthServices
{
    //if you are using a db table, you would inject the dbcontext inside
    //the service and you would query the apikeys table for the value,
    //and if there is expiration, you would make sure it hasnt expired


    public class ApiKeyValidatorService(NoName_hotel_DbContext dbContext) : IApiKeyValidatorService
    //public class ApiKeyValidatorService(IConfiguration configuration) : IApiKeyValidatorService
    {
        public async Task<bool> IaValidAsync(string ApiKey, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(ApiKey)) return false;

            var apiKeyEntity = await dbContext.ApiKeys
                .AsNoTracking()
                .FirstOrDefaultAsync(k  => k.Key == ApiKey, ct);

            if(apiKeyEntity == null) return false;

            return apiKeyEntity.IsActive;
        }
    }
}
