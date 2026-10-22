using NoName_Hotel.Application.DTO.AuthDto;
using NoName_Hotel.Common.Results;

namespace NoName_Hotel.Application.Contracts.AuthContracts
{
    public interface IUserServices
    {
        string UserId { get; }

        Task<Result<string>> LoginUserAsync(LoginUserDto loginUserDto);
        Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registeredUserDto);
    }
}