using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.api.Constants;
using NoName_Hotel.api.Contracts.AuthContracts;
using NoName_Hotel.api.DTO.AuthDto;
using NoName_Hotel.api.Entities.Models;
using NoName_Hotel.api.Results;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace NoName_Hotel.api.Services.AuthServices 
{
    public class UserServices(UserManager<ApplicationUser> userManager, IConfiguration configuration) : IUserServices
    {
        public async Task<Result<RegisteredUserDto>> RegisterAsync(RegisterUserDto registeredUserDto)
        {
            var user = new ApplicationUser
            {
                Email = registeredUserDto.Email,
                FirstName = registeredUserDto.FirstName,
                LastName = registeredUserDto.LastName,
                UserName = registeredUserDto.Email
            };

            var result = await userManager.CreateAsync(user, registeredUserDto.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => new Error(ErrorCodes.BadRequest, e.Description)).ToArray();
                return Result<RegisteredUserDto>.BadRequest(errors);
            }

            await userManager.AddToRoleAsync(user, registeredUserDto.Role);

            var registerUserdto = new RegisteredUserDto
            {
                Email = user.Email,
                Firstname = user.FirstName,
                Lastname = user.LastName,
                Id = user.Id,
                Role = registeredUserDto.Role,
            };

            return Result<RegisteredUserDto>.Success(registerUserdto);

        }
        public async Task<Result<string>> LoginUserAsync(LoginUserDto loginUserDto)
        {
            var user = await userManager.FindByEmailAsync(loginUserDto.Email);

            if (user == null)
            {
                return Result<string>.Failure(new Error(ErrorCodes.BadRequest, "Invalid credentials."));
            }

            var valid = await userManager.CheckPasswordAsync(user, loginUserDto.Password);
            if (!valid)
            {
                return Result<string>.Failure(new Error(ErrorCodes.BadRequest, "invalid credential"));
            }

            //issue token
            var token = await GenerateToken(user);

            return Result<string>.Success(token);
        }

        public async Task<string> GenerateToken(ApplicationUser user)
        {
            //setting basic user claims
            var claims = new List<Claim>
            {
                new (JwtRegisteredClaimNames.Sub, user.Id),
                new (JwtRegisteredClaimNames.Email, user.Email),
                new (JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new (JwtRegisteredClaimNames.Name, user.FullName)
            };

            //setting user role claims
            var roles = await userManager.GetRolesAsync(user);
            var roleCalims = roles.Select(x => new Claim(ClaimTypes.Role, x)).ToList();

            claims = claims.Union(roleCalims).ToList();

            //setting jwt key credentials
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["jwtSettings.key"] ?? string.Empty));
            var credentails = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            //creating an exncoded token
            var token = new JwtSecurityToken(
                 issuer: configuration["jwtSettings.Issuer"],
                  audience: configuration["jwtSettings.Audeince"],
                  claims: claims,
                  expires : DateTime.UtcNow.AddMinutes(Convert.ToInt32(configuration["jwtSettings.DurationInMinutes"])),
                  signingCredentials : credentails
            );

            //returning token value
            return new JwtSecurityTokenHandler().WriteToken(token);

        }
    }
}
