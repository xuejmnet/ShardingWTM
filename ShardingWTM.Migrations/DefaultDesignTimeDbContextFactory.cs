using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using ShardingCore;
using ShardingCore.Bootstrappers;
using ShardingCore.Core.DbContextCreator;
using ShardingCore.TableExists;
using ShardingWTM.EFCore;
using ShardingWTM.EFCore.Sharding;
using WalkingTec.Mvvm.Core;

namespace ShardingWTM.Migrations;

public class DefaultDesignTimeDbContextFactory: IDesignTimeDbContextFactory<DataContext>
{
    public static readonly ILoggerFactory efLogger = LoggerFactory.Create(builder =>
    {
        builder.AddFilter((category, level) => category == DbLoggerCategory.Database.Command.Name && level == LogLevel.Information).AddConsole();
    });
    static DefaultDesignTimeDbContextFactory()
    {
        var services = new ServiceCollection();
        
        services.AddScoped<DataContext>(sp =>
        {
            var dbContextOptionsBuilder = new DbContextOptionsBuilder<DataContext>();
            dbContextOptionsBuilder.UseMySql(
                "server=127.0.0.1;port=3306;database=shardingTest;userid=root;password=L6yBtV6qNENrwBy7;",
                new MySqlServerVersion(new Version()));
            dbContextOptionsBuilder.UseSharding<DataContext>();
            return new DataContext(dbContextOptionsBuilder.Options);
        });
        services.AddShardingConfigure<DataContext>()
            .AddEntityConfig(o =>
            {
                //o.CreateShardingTableOnStart = true;
                //o.EnsureCreatedWithOutShardingTable = true;
                o.CreateDataBaseOnlyOnStart = true;
                o.AddShardingTableRoute<TodoRoute>();
            })
            .AddConfig(o =>
            {
                o.AddDefaultDataSource("ds0",
                    "server=127.0.0.1;port=3306;database=shardingTest;userid=root;password=L6yBtV6qNENrwBy7;");
                o.ConfigId = "c1";
                o.UseShellDbContextConfigure(builder =>
                {
                    builder.ReplaceService<IMigrationsSqlGenerator, ShardingMySqlMigrationSqlGenerator<DataContext>>();
                });
                o.UseShardingQuery((conn, build) =>
                {
                    build.UseMySql(conn, new MySqlServerVersion(new Version()),op=>op.MigrationsAssembly("ShardingWTM.EFCore")).UseLoggerFactory(efLogger);
                });
                o.UseShardingTransaction((conn,build)=>
                    build.UseMySql(conn,new MySqlServerVersion(new Version())).UseLoggerFactory(efLogger)
                );
                o.ReplaceTableEnsureManager(sp => new MySqlTableEnsureManager<DataContext>());
            }).EnsureConfig();
            
        services.Replace(ServiceDescriptor.Singleton<IDbContextCreator<DataContext>, WTMDbContextCreator<DataContext>>());
        services.Replace(ServiceDescriptor.Scoped<IDataContext>(sp =>
        {
            return sp.GetService<DataContext>();
        }));
        var buildServiceProvider = services.BuildServiceProvider();
        ShardingContainer.SetServices(buildServiceProvider);
        ShardingContainer.GetService<IShardingBootstrapper>().Start();
    }
    public DataContext CreateDbContext(string[] args)
    {
        return ShardingContainer.GetService<DataContext>();
    }
}