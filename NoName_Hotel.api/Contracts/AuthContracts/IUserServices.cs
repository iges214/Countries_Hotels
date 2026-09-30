using NoName_Hotel.api.DTO.AuthDto;
using NoName_Hotel.api.Results;

namespace NoName_Hotel.api.Contracts.AuthContracts
{
    public interface IUserServices
    {
        Task<Result<string>> LoginUserAsync(LoginUserDto loginUserDto);
        Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registeredUserDto);
    }
}