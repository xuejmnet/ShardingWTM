using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure.Internal;
using Pomelo.EntityFrameworkCore.MySql.Migrations;
using ShardingCore.Helpers;
using ShardingCore.Sharding.Abstractions;

namespace ShardingWTM.EFCore;

public class ShardingMySqlMigrationSqlGenerator<TShardingDbContext>:MySqlMigrationsSqlGenerator where TShardingDbContext:DbContext,IShardingDbContext
{
    public ShardingMySqlMigrationSqlGenerator(MigrationsSqlGeneratorDependencies dependencies, IRelationalAnnotationProvider annotationProvider, IMySqlOptions options) : base(dependencies, annotationProvider, options)
    {
    }
    protected override void Generate(
        MigrationOperation operation,
        IModel model,
        MigrationCommandListBuilder builder)
    {
        var oldCmds = builder.GetCommandList().ToList();
        base.Generate(operation, model, builder);
        var newCmds = builder.GetCommandList().ToList();
        var addCmds = newCmds.Where(x => !oldCmds.Contains(x)).ToList();

        MigrationHelper.Generate<TShardingDbContext>(operation, builder, Dependencies.SqlGenerationHelper, addCmds);
    }
}