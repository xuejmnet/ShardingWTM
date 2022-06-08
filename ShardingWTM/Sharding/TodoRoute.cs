using ShardingCore.Core.EntityMetadatas;
using ShardingCore.VirtualRoutes.Mods;
using WalkingTec.Mvvm.Core;

namespace ShardingWTM.Sharding
{

    public class TodoRoute:AbstractSimpleShardingModKeyStringVirtualTableRoute<Todo>
    {
        public TodoRoute() : base(2, 10)
        {
        }

        public override void Configure(EntityMetadataTableBuilder<Todo> builder)
        {
            builder.ShardingProperty(o => o.Id);
        }
    }
}