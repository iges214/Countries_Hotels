using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.api.Constants;
using NoName_Hotel.api.Contracts;
using NoName_Hotel.api.Contracts.AuthContracts;
using NoName_Hotel.api.Data;
using NoName_Hotel.api.Entities.Models;
using NoName_Hotel.api.Handlers;
using NoName_Hotel.api.Mappers;
using NoName_Hotel.api.Services;
using NoName_Hotel.api.Services.AuthServices;
using System.Text;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

var connectionString = builder.Configuration.GetConnectionString("NoName_HotelDbConnectionString");
builder.Services.AddDbContext<NoName_hotel_DbContext>(options => options.UseSqlServer(connectionString));

//builder.Services.AddIdentityCore<ApplicationUser>(options =>{ })
//.AddRoles<IdentityRole>()
//.AddEntityFrameworkStores<NoName_hotel_DbContext>()
//.AddDefaultTokenProviders();
builder.Services.AddIdentityApiEndpoints<ApplicationUser>()
    .AddRoles<IdentityRole>()
.AddEntityFrameworkStores<NoName_hotel_DbContext>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["jwtSettings.Issuer"],
            ValidAudience = builder.Configuration["jwtSettings.Audeince"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["jwtSettings.key"] ?? string.Empty)),
            ClockSkew = TimeSpan.Zero //default is 5min
        };
    })
    //.AddScheme<AuthenticationSchemeOptions, BaseAuthenticationHandler>(AuthenticationDefault.Basicscheme, _ => { });
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthenticationDefault.ApiKeyScheme, _ => { });


builder.Services.AddAuthorization();

//IG>eloosandra123

builder.Services.AddScoped<ICountriesService, CountriesService>();
builder.Services.AddScoped<IHotelService, HotelService>();
builder.Services.AddScoped<IUserServices, UserServices>();
builder.Services.AddScoped<IApiKeyValidatorService, ApiKeyValidatorService>();

builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<HotelMappingProfile>();
    cfg.AddProfile<CountryMappingProfile>();
});

builder.Services.AddControllers()
    .AddJsonOptions(configure =>
    {
        configure.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
app.MapGroup("api/default").MapIdentityApi<ApplicationUser>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
