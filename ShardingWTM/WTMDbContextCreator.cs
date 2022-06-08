using Microsoft.EntityFrameworkCore;
using ShardingCore.Core.DbContextCreator;
using ShardingCore.Sharding.Abstractions;

namespace ShardingWTM;

public class WTMDbContextCreator<TShardingDbContext>:IDbContextCreator<TShardingDbContext>  where TShardingDbContext : DbContext, IShardingDbContext
{
    public DbContext CreateDbContext(DbContext shellDbContext, ShardingDbContextOptions shardingDbContextOptions)
    {
        var context = new DataContext((DbContextOptions<DataContext>)shardingDbContextOptions.DbContextOptions);
        context.RouteTail = shardingDbContextOptions.RouteTail;
        return context;
    }
}