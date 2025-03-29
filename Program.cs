// See https://aka.ms/new-console-template for more information
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

internal class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("Hello, World!");

        // ConnectionsController.cs
        //[ApiController]
        //[Route("api/[controller]")]
        //public class ConnectionsController : ControllerBase
        //{
        //    private readonly UserConnectionService _service;

        //    public ConnectionsController(UserConnectionService service)
        //    {
        //        _service = service;
        //    }

        //    [HttpPost]
        //    public async Task<IActionResult> AddConnection([FromBody] ConnectionRequest request)
        //    {
        //        await _service.ProcessConnectionEventAsync(request.UserId, request.IP);
        //        return Accepted();
        //    }

        //    [HttpGet("search")]
        //    public async Task<IActionResult> SearchUsersByIp([FromQuery] string ipPrefix)
        //    {
        //        var users = await _service.FindUsersByIpPrefixAsync(ipPrefix);
        //        return Ok(users);
        //    }

        //    [HttpGet("{userId}/ips")]
        //    public async Task<IActionResult> GetUserIps(long userId)
        //    {
        //        var ips = await _service.GetUserIpsAsync(userId);
        //        return Ok(ips);
        //    }

        //    [HttpGet("{userId}/last-connection")]
        //    public async Task<IActionResult> GetLastConnection(long userId)
        //    {
        //        var connection = await _service.GetLastConnectionAsync(userId);
        //        return Ok(connection);
        //    }
        //}

        // Program.cs




        try
        {
            var host = Host.CreateDefaultBuilder(args)
                //.ConfigureAppConfiguration((hostContext, configApp) =>
                //{
                //    configApp.AddConfiguration(configuration);
                //})
                .ConfigureServices((hostContext, services) =>
                {
                    services.Configure<HostOptions>(o => o.ShutdownTimeout = TimeSpan.FromMinutes(1));
                    services.AddHostedService<Worker>();
                    services.AddSingleton(Log.Logger);
                })
                //.UseSerilog()
                .Build();

            await host.RunAsync();
        }
        catch (Exception e)
        {
            Log.Error(e, e.Message);
        }

        Log.Warning("Dell audit agent stopped");
        Log.CloseAndFlush();
    }
}

// UserConnectionEvent.cs
public class UserConnectionEvent
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string IP { get; set; }
    public DateTime Timestamp { get; set; }
}

// LastUserConnection.cs
public class LastUserConnection
{
    public long UserId { get; set; }
    public string LastIP { get; set; }
    public DateTime LastConnectionTime { get; set; }
}

// AppDbContext.cs
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserConnectionEvent> UserConnectionEvents { get; set; }
    public DbSet<LastUserConnection> LastUserConnections { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserConnectionEvent>()
            .HasIndex(e => new { e.UserId, e.Timestamp });

        modelBuilder.Entity<LastUserConnection>()
            .HasKey(l => l.UserId);
    }
}

// IUserConnectionRepository.cs
public interface IUserConnectionRepository
{
    Task AddConnectionEventAsync(UserConnectionEvent @event);
    Task UpdateLastConnectionAsync(LastUserConnection connection);
    Task<List<long>> FindUsersByIpPrefixAsync(string ipPrefix);
    Task<List<string>> GetUserIpsAsync(long userId);
    Task<LastUserConnection> GetLastConnectionAsync(long userId);
}

// UserConnectionRepository.cs
public class UserConnectionRepository : IUserConnectionRepository
{
    private readonly AppDbContext _context;

    public UserConnectionRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddConnectionEventAsync(UserConnectionEvent @event)
    {
        await _context.UserConnectionEvents.AddAsync(@event);
        await _context.SaveChangesAsync();
    }

    //public async Task UpdateLastConnectionAsync(LastUserConnection connection)
    //{
    //    await _context.LastUserConnections
    //        .Upsert(connection)
    //        .On(u => u.UserId)
    //        .WhenMatched(u => new LastUserConnection
    //        {
    //            LastIP = connection.LastIP,
    //            LastConnectionTime = connection.LastConnectionTime
    //        })
    //        .RunAsync();
    //}

    public async Task<List<long>> FindUsersByIpPrefixAsync(string ipPrefix)
    {
        return await _context.UserConnectionEvents
            .Where(e => EF.Functions.Like(e.IP, $"{ipPrefix}%"))
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<List<string>> GetUserIpsAsync(long userId)
    {
        return await _context.UserConnectionEvents
            .Where(e => e.UserId == userId)
            .Select(e => e.IP)
            .Distinct()
            .ToListAsync();
    }

    public async Task<LastUserConnection> GetLastConnectionAsync(long userId)
    {
        return await _context.LastUserConnections
            .FirstOrDefaultAsync(l => l.UserId == userId);
    }

    public Task UpdateLastConnectionAsync(LastUserConnection connection)
    {
        throw new NotImplementedException();
    }
}

// UserConnectionService.cs
public class UserConnectionService
{
    private readonly IUserConnectionRepository _repository;
    private readonly ILogger<UserConnectionService> _logger;

    public UserConnectionService(
        IUserConnectionRepository repository,
        ILogger<UserConnectionService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task ProcessConnectionEventAsync(long userId, string ip)
    {
        var timestamp = DateTime.UtcNow;
        var connectionEvent = new UserConnectionEvent
        {
            UserId = userId,
            IP = ip,
            Timestamp = timestamp
        };

        var lastConnection = new LastUserConnection
        {
            UserId = userId,
            LastIP = ip,
            LastConnectionTime = timestamp
        };

        try
        {
            await _repository.AddConnectionEventAsync(connectionEvent);
            await _repository.UpdateLastConnectionAsync(lastConnection);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing connection event");
            throw;
        }
    }
}

//var builder = WebApplication.CreateBuilder(args);

//// Configure database
//builder.Services.AddDbContext<AppDbContext>(options =>
//    options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"))
//    .UseSnakeCaseNamingConvention();

//// Add services
//builder.Services.AddScoped<IUserConnectionRepository, UserConnectionRepository>();
//builder.Services.AddScoped<UserConnectionService>();

//// Configure high performance
//builder.Services.AddDatabaseDeveloperPageExceptionFilter();
//builder.Services.AddHealthChecks()
//    .AddNpgSql(builder.Configuration.GetConnectionString("PostgreSQL"));

//var app = builder.Build();

//// Database migrations
//using (var scope = app.Services.CreateScope())
//{
//    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
//    await db.Database.MigrateAsync();
//}

//app.MapControllers();
//app.MapHealthChecks("/health");
//app.Run();