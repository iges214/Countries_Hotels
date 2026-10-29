using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using NoName_Hotel.api.Controllers.CachePolicies;
using NoName_Hotel.api.Handlers;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.Mappers;
using NoName_Hotel.Application.Services;
using NoName_Hotel.Application.Services.AuthServices;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.JWT;
using NoName_Hotel.Domain.Data;
using NoName_Hotel.Domain.Entities.Models;
using Serilog;
using Serilog.Events;
using System.Text;
using System.Threading.RateLimiting;


Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day)
    .CreateBootstrapLogger();

try
{

    Log.Information("Starting Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    // Add services to the container.

    var connectionString = builder.Configuration.GetConnectionString("NoName_HotelDbConnectionString");

    builder.Services.AddDbContextPool<NoName_hotel_DbContext>(options =>
    {
        options.UseSqlServer(connectionString, sqloptions =>
        {
            sqloptions.CommandTimeout(30);
            sqloptions.EnableRetryOnFailure(
                maxRetryCount: 3,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null
                );
        });

        if (builder.Environment.IsDevelopment())
        {
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        }

        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    }, poolSize: 250);


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
        throw new InvalidOperationException(
            "JWT signing key is not configured. Set 'JwtSettings:key' in user secrets " +
            "for Development or set the 'JwtSettings__key' environment variable.");
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
        .AddNewtonsoftJson()
        .AddJsonOptions(configure =>
        {
            configure.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //builder.Services.AddMemoryCache();    IN-MEMORY CACHING e.g RAM
    builder.Services.AddOutputCache(options =>
    {
        options.AddPolicy(CacheConstants.AuthenticatedUserCachingPolicy, builder => {
            builder.AddPolicy<AuthenticatedUserCachingPolicy>()
            .SetCacheKeyPrefix(CacheConstants.AuthenticatedUserCachingPolicy)
            .Expire(TimeSpan.FromSeconds(CacheConstants.MediumDuration))
                .Tag(CacheConstants.CatalogTag);
        }, excludeDefaultPolicy: true);
    });

    //RATE LIMITER
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("fixed", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(5);
            opt.PermitLimit = 10;
            opt.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            opt.QueueLimit = 10;
        });

        options.AddPolicy("perUser", context =>
        {
            var userName = context.User?.Identity?.Name ?? "anonymous";

            return RateLimitPartition.GetSlidingWindowLimiter(userName, _ => new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 50,
                SegmentsPerWindow = 5,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
        });

        //global limiter
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        {
            var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1),
                PermitLimit = 200,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        options.OnRejected = async (context, CancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();
            }

            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json";

            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                error = "Too many request",
                message = "Rate limit exceeeded, please try again later",
                retryAfter = retryAfter.TotalSeconds
            }, cancellationToken: CancellationToken);

        };
    });


    var app = builder.Build();
    app.MapGroup("api/default").MapIdentityApi<ApplicationUser>();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();

    app.UseRateLimiter();

    app.UseAuthorization();

    app.UseOutputCache();

    app.MapControllers();

    Log.Information("HotelApp API started successfully");

    app.Run();

}
catch (Exception e)
{
    Log.Fatal(e, "API terminated unexpectedly");
    Environment.ExitCode = 1;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
