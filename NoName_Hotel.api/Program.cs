using Asp.Versioning;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using NoName_Hotel.api.Controllers.CachePolicies;
using NoName_Hotel.api.Handlers;
using NoName_Hotel.api.MiddleWare;
using NoName_Hotel.Application.Contracts;
using NoName_Hotel.Application.Contracts.AuthContracts;
using NoName_Hotel.Application.Mappers;
using NoName_Hotel.Application.Services;
using NoName_Hotel.Application.Services.AuthServices;
using NoName_Hotel.Common.Constants;
using NoName_Hotel.Common.JWT;
using NoName_Hotel.Infrastructure.Data;
using NoName_Hotel.Infrastructure.Repositories;
using NoName_Hotel.Infrastructure.Storage;
using NoName_Hotel.api.Filters;
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

        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);

    }, poolSize: 250);


    //builder.Services.AddIdentityCore<ApplicationUser>(options =>{ })
    //.AddRoles<IdentityRole>()
    //.AddEntityFrameworkStores<NoName_hotel_DbContext>()
    //.AddDefaultTokenProviders();
    builder.Services.AddIdentityApiEndpoints<ApplicationUser>(options =>
    {
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
    })
        .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<NoName_hotel_DbContext>();
    builder.Services.AddScoped<IUserStore<ApplicationUser>, TrackingUserStore>();

    builder.Services.AddHttpContextAccessor();

    builder.Services.AddOptions<JwtSettings>()
        .Bind(builder.Configuration.GetSection("JwtSettings"))
        .Validate(settings => !string.IsNullOrWhiteSpace(settings.key) && Encoding.UTF8.GetByteCount(settings.key) >= 32,
            "JWT signing key must contain at least 32 UTF-8 bytes. Configure user secrets or JwtSettings__key.")
        .Validate(settings => int.TryParse(settings.DurationInMinutes, out var minutes) && minutes > 0,
            "JWT duration must be a positive number of minutes.")
        .ValidateOnStart();

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    })
        .AddJwtBearer()
        //.AddScheme<AuthenticationSchemeOptions, BaseAuthenticationHandler>(AuthenticationDefault.Basicscheme, _ => { });
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(AuthenticationDefault.ApiKeyScheme, _ => { });


    builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<Microsoft.Extensions.Options.IOptions<JwtSettings>>((options, configured) =>
        {
            var settings = configured.Value;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
                ValidIssuer = settings.Issuer, ValidAudience = settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.key)),
                ClockSkew = TimeSpan.Zero
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var stamp = context.Principal?.FindFirst("security_stamp")?.Value;
                    if (stamp is null) return; // Compatibility with previously issued sessions.
                    var manager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var id = context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var user = id is null ? null : await manager.FindByIdAsync(id);
                    if (user is null || user.SecurityStamp != stamp)
                    {
                        context.Fail("The session has been revoked.");
                        return;
                    }

                    var sessionId = context.Principal?.FindFirst(BrowserSessionService.SessionIdClaim)?.Value;
                    var sessions = context.HttpContext.RequestServices.GetRequiredService<BrowserSessionService>();
                    if (sessionId is not null && (!Guid.TryParse(sessionId, out var sid)
                        || !await sessions.IsActiveAsync(sid, user.Id)))
                        context.Fail("The session has expired or been revoked.");
                }
            };
        });

    builder.Services.AddAuthorization();

    //IG>eloosandra123

    builder.Services.AddScoped<ICountriesService, CountriesService>();
    builder.Services.AddScoped<IHotelService, HotelService>();
    builder.Services.AddScoped<IUserServices, UserServices>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddScoped<BrowserSessionService>();
    builder.Services.AddScoped<IApiKeyValidatorService, ApiKeyValidatorService>();
    builder.Services.AddScoped<IBookingService, BookingService>();
    builder.Services.AddScoped<IHotelRepository, HotelRepository>();
    builder.Services.AddScoped<IBranchCatalogService, BranchCatalogService>();
    builder.Services.AddScoped<IPhotoStorage, LocalPhotoStorage>();
    builder.Services.AddScoped<AdministrationService>();
    builder.Services.AddScoped<IHotelMailSender, HotelMailSender>();
    builder.Services.AddScoped<CatalogCacheInvalidationFilter>();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();


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
    builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "IGES Hotel and Suites API", Version = "v1",
        Description = "Branch selection, room availability, reservations and hospitality catalogues."
    }));
    //builder.Services.AddSwaggerGen(opt =>
    //{
    //    //api info
    //    opt.SwaggerDoc("v1", new OpenApiInfo
    //    {
    //        Version = "v1",
    //        Title = "NoName_Hotel Api",
    //        Description = "Api for managing hotels, countries and bookings",
    //        Contact = new OpenApiContact
    //        {
    //            Name = "Support Team",
    //            Email = "Support@Noname_hotel.com"
    //        },
    //        License = new OpenApiLicense
    //        {
    //            Name = "MIT License",
    //            Url = new Uri("https://opensource.org/licenses/MIT")
    //        }
    //    });

    //    opt.SwaggerDoc("v2", new OpenApiInfo
    //    {
    //        Version = "v2",
    //        Title = "NoName_Hotel Api",
    //        Description = "version 2 of the hotel api with enhances features"
    //    });


    //});

    //builder.Services.AddMemoryCache();    IN-MEMORY CACHING e.g RAM
    builder.Services.AddOutputCache(options =>
    {
        options.AddPolicy(CacheConstants.AuthenticatedUserCachingPolicy, builder =>
        {
            builder.AddPolicy<AuthenticatedUserCachingPolicy>()
            .SetCacheKeyPrefix(CacheConstants.AuthenticatedUserCachingPolicy)
            .Expire(TimeSpan.FromSeconds(CacheConstants.MediumDuration))
                .Tag(CacheConstants.CatalogTag);
        }, excludeDefaultPolicy: true);
    });

    //RATE LIMITER
    builder.Services.AddRateLimiter(options =>
    {
        options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1), PermitLimit = 5, QueueLimit = 0
            }));
        options.AddPolicy("contact", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromMinutes(1), PermitLimit = 5, QueueLimit = 0
            }));
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

    builder.Services.AddHealthChecks()
        .AddCheck("self", () => HealthCheckResult.Healthy("Application is running"), tags: ["api"])
        .AddDbContextCheck<NoName_hotel_DbContext>(
             name: "database",
             failureStatus: HealthStatus.Unhealthy,
             tags: ["db", "sql"]);

    builder.Services.AddHealthChecksUI(setup =>
    {
        setup.SetEvaluationTimeInSeconds(10); //checks eveery 10secs
        setup.MaximumHistoryEntriesPerEndpoint(50);
        setup.AddHealthCheckEndpoint("NoName_Hotel API", "/healths");
    }).AddInMemoryStorage();

    builder.Services.AddApiVersioning(opt =>
    {
        opt.AssumeDefaultVersionWhenUnspecified = true;
        opt.DefaultApiVersion = new ApiVersion(1, 0);
        opt.ReportApiVersions = true;
        opt.ApiVersionReader = new UrlSegmentApiVersionReader();
    }).AddMvc().AddApiExplorer(opt =>
    {
        opt.GroupNameFormat = "'v'VVV";
        opt.SubstituteApiVersionInUrl = true;
    });

    var app = builder.Build();

    // Explicit operator command; never a web endpoint or an automatic startup grant.
    var grantIndex = Array.IndexOf(args, "--grant-system-admin");
    if (grantIndex >= 0)
    {
        if (grantIndex + 1 >= args.Length) throw new ArgumentException("Supply the email of an existing registered account after --grant-system-admin.");
        using var scope = app.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByEmailAsync(args[grantIndex + 1]) ?? throw new InvalidOperationException("Register this account before granting system administrator access.");
        var grant = await manager.AddToRoleAsync(user, RoleNames.Administrator);
        if (!grant.Succeeded) throw new InvalidOperationException(string.Join(" ", grant.Errors.Select(e => e.Description)));
        Log.Information("System administrator access granted to account {UserId}", user.Id);
        return;
    }

    app.UseExceptionHandler();

    app.UseSerilogRequestLogging(opt =>
    {
        opt.MessageTemplate = "HTTP {RequestMethod} {RequestPath} Responded {StatusCode} in {Elapsed:0.0000}ms ";

        opt.GetLevel = (httpContext, elapsed, ex) => ex != null
        ? LogEventLevel.Error
        : httpContext.Response.StatusCode >= 500
            ? LogEventLevel.Error
            : httpContext.Response.StatusCode >= 400
                 ? LogEventLevel.Warning
                 : LogEventLevel.Information;

        opt.EnrichDiagnosticContext = (dianosticContext, HttpContext) =>
        {
            dianosticContext.Set("UserName", HttpContext.User?.Identity?.Name ?? "anonymouse");

            dianosticContext.Set("RemoteIP", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

            if (HttpContext.User?.Identity?.IsAuthenticated == true)
            {
                dianosticContext.Set("UserId", HttpContext.User.FindFirst("sub")?.Value ?? "unknown");
            }
        };
    });

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    var imageWebRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(Path.Combine(imageWebRoot, "uploads"));
    app.Environment.WebRootPath = imageWebRoot;
    var imageFiles = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(imageWebRoot);
    app.Environment.WebRootFileProvider = imageFiles;
    app.Lifetime.ApplicationStopped.Register(imageFiles.Dispose);
    app.UseStaticFiles();

    //app.MapHealthChecks("/healths", new HealthCheckOptions
    //{
    //    ResponseWriter = async (context, report) =>
    //    {
    //        context.Response.ContentType = "application/json";

    //        var response = new
    //        {
    //            status = report.Status.ToString(),
    //            checks = report.Entries.Select(entry => new
    //            {
    //                name = entry.Key,
    //                status = entry.Value.Status.ToString(),
    //                description = entry.Value.Description,
    //                duration = entry.Value.Duration.TotalMilliseconds,
    //                exception = entry.Value.Exception?.Message,
    //                data = entry.Value.Data

    //            }),

    //        totalDuration = report.TotalDuration.TotalMilliseconds
    //        };

    //        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
    //        {
    //            WriteIndented = true
    //        }));
    //}

    //});

    app.MapHealthChecks("/healths", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapHealthChecks("/healths/live", new HealthCheckOptions
    {
        Predicate = _ => false
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("db") });

    app.MapHealthChecks("/healths/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("db")
    });

    app.MapHealthChecksUI(opt =>
    {
        opt.UIPath = "/healthchecks-ui";
        opt.ApiPath = "/healthchecks-Api";
    });



    app.UseAuthentication();

    app.UseRateLimiter();

    app.UseAuthorization();

    app.UseOutputCache();

    app.MapControllers();

    // Let React handle browser routes while preserving 404s for missing API routes and assets.
    app.MapFallbackToFile("/", "index.html");
    app.MapFallbackToFile(
        "{*path:nonfile:regex(^(?!(api|assets|uploads|health|healths|healthchecks-ui|healthchecks-api)(/|$)))}",
        "index.html");

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
