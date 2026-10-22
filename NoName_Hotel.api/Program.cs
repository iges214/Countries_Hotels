using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.api.Handlers;
using NoName_Hotel.Application.Mappers;
using NoName_Hotel.Application.Services;
using System.Text;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.Services.AuthServices;
using NoName_Hotel.Common.JWT;


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

builder.Services.AddHttpContextAccessor();

builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));

var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();

if (string.IsNullOrWhiteSpace(jwtSettings.key))
{
    throw new InvalidOperationException("Jwt key is not configured");
}



builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.key)),
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
builder.Services.AddScoped<IBookingService, BookingService>();


//builder.Services.AddAutoMapper(cfg => { }, Assembly.GetExecutingAssembly());
builder.Services.AddAutoMapper(cfg =>
{
    cfg.AddProfile<HotelMappingProfile>();
    cfg.AddProfile<CountryMappingProfile>();
    cfg.AddProfile<BookingMappingProfile>();
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
