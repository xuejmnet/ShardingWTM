using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShardingCore;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources;
using ShardingCore.Core.VirtualRoutes.TableRoutes.RouteTails.Abstractions;
using ShardingCore.EFCores.OptionsExtensions;
using ShardingCore.Extensions;
using ShardingCore.Sharding.Abstractions;
using ShardingCore.Sharding.ShardingDbContextExecutors;
using WalkingTec.Mvvm.Core;
using DbContext = Microsoft.EntityFrameworkCore.DbContext;
using DbContextOptions = Microsoft.EntityFrameworkCore.DbContextOptions;

namespace ShardingWTM
{

    public abstract class AbstractShardingFrameworkContext:FrameworkContext, IShardingDbContext, ISupportShardingReadWrite
    {
        protected IShardingDbContextExecutor ShardingDbContextExecutor
        {
            get;
        }

        public AbstractShardingFrameworkContext(CS cs)
            : base(cs)
        {
            
            ShardingDbContextExecutor =
                (IShardingDbContextExecutor)Activator.CreateInstance(
                    typeof(ShardingDbContextExecutor<>).GetGenericType0(this.GetType()),this);
            IsExecutor = false;
        }
        
        public AbstractShardingFrameworkContext(string cs, DBTypeEnum dbtype)
            : base(cs, dbtype)
        {
            ShardingDbContextExecutor =
                (IShardingDbContextExecutor)Activator.CreateInstance(
                    typeof(ShardingDbContextExecutor<>).GetGenericType0(this.GetType()),this);
            IsExecutor = false;
        }
        
        public AbstractShardingFrameworkContext(string cs, DBTypeEnum dbtype, string version = null)
            : base(cs, dbtype, version)
        {
            ShardingDbContextExecutor =
                (IShardingDbContextExecutor)Activator.CreateInstance(
                    typeof(ShardingDbContextExecutor<>).GetGenericType0(this.GetType()),this);
            IsExecutor = false;
        }

        public AbstractShardingFrameworkContext(DbContextOptions options) : base(options)
        {
            var wrapOptionsExtension = options.FindExtension<ShardingWrapOptionsExtension>();
            if (wrapOptionsExtension != null)
            {
                ShardingDbContextExecutor =
                    (IShardingDbContextExecutor)Activator.CreateInstance(
                        typeof(ShardingDbContextExecutor<>).GetGenericType0(this.GetType()),this);
            }

            IsExecutor = wrapOptionsExtension == null;
        }
        
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (this.CSName!=null)
            {
                base.OnConfiguring(optionsBuilder);
                optionsBuilder.UseSharding<DataContext>();
            }
        }
        /// <summary>
        /// 读写分离优先级
        /// </summary>
        public int ReadWriteSeparationPriority
        {
            get => ShardingDbContextExecutor.ReadWriteSeparationPriority;
            set => ShardingDbContextExecutor.ReadWriteSeparationPriority = value;
        }
        /// <summary>
        /// 是否使用读写分离
        /// </summary>
        public bool ReadWriteSeparation
        {
            get => ShardingDbContextExecutor.ReadWriteSeparation;
            set => ShardingDbContextExecutor.ReadWriteSeparation = value;
        }

        /// <summary>
        /// 是否是真正的执行者
        /// </summary>
        public bool IsExecutor { get;}



        public DbContext GetDbContext(string dataSourceName, bool parallelQuery, IRouteTail routeTail)
        {
            return ShardingDbContextExecutor.CreateDbContext(parallelQuery, dataSourceName, routeTail);
        }

        /// <summary>
        /// 根据对象创建通用的dbcontext
        /// </summary>
        /// <typeparam name="TEntity"></typeparam>
        /// <param name="entity"></param>
        /// <returns></returns>
        public DbContext CreateGenericDbContext<TEntity>(TEntity entity) where TEntity : class
        {
            return ShardingDbContextExecutor.CreateGenericDbContext(entity);
        }

        public IVirtualDataSource GetVirtualDataSource()
        {
            return ShardingDbContextExecutor.GetVirtualDataSource();
        }


        public override EntityEntry Add(object entity)
        {
            if (IsExecutor)
                base.Add(entity);
            return CreateGenericDbContext(entity).Add(entity);
        }

        public override EntityEntry<TEntity> Add<TEntity>(TEntity entity)
        {
            if (IsExecutor)
                return base.Add(entity);
            return CreateGenericDbContext(entity).Add(entity);
        }

        public override ValueTask<EntityEntry<TEntity>> AddAsync<TEntity>(TEntity entity, CancellationToken cancellationToken = new CancellationToken())
        {
            if (IsExecutor)
                return base.AddAsync(entity, cancellationToken);
            return CreateGenericDbContext(entity).AddAsync(entity, cancellationToken);
        }

        public override ValueTask<EntityEntry> AddAsync(object entity, CancellationToken cancellationToken = new CancellationToken())
        {
            if (IsExecutor)
                return base.AddAsync(entity, cancellationToken);
            return CreateGenericDbContext(entity).AddAsync(entity, cancellationToken);
        }

        private Dictionary<DbContext, IEnumerable<TEntity>> AggregateToDic<TEntity>(IEnumerable<TEntity> entities) where TEntity:class
        {
            return entities.Select(o =>
            {
                var dbContext = CreateGenericDbContext(o);
                return new
                {
                    DbContext = dbContext,
                    Entity = o
                };
            }).GroupBy(g => g.DbContext).ToDictionary(o => o.Key, o => o.Select(g => g.Entity));
        }
        public override void AddRange(params object[] entities)
        {
            if (IsExecutor)
            {
                base.AddRange(entities);
                return;
            }

            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.AddRange(aggregateKv.Value);
            }
        }

        public override void AddRange(IEnumerable<object> entities)
        {
            if (IsExecutor)
            {
                base.AddRange(entities);
                return;
            }

            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.AddRange(aggregateKv.Value);
            }
        }

        public override async Task AddRangeAsync(params object[] entities)
        {
            if (IsExecutor)
            {
                await base.AddRangeAsync(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                await aggregateKv.Key.AddRangeAsync(aggregateKv.Value);
            }
        }

        public override async Task AddRangeAsync(IEnumerable<object> entities, CancellationToken cancellationToken = new CancellationToken())
        {
            if (IsExecutor)
            {
                await base.AddRangeAsync(entities, cancellationToken);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                await aggregateKv.Key.AddRangeAsync(aggregateKv.Value,cancellationToken);
            }
        }

        public override EntityEntry<TEntity> Attach<TEntity>(TEntity entity)
        {
            if (IsExecutor)
                return base.Attach(entity);
            return CreateGenericDbContext(entity).Attach(entity);
        }

        public override EntityEntry Attach(object entity)
        {
            if (IsExecutor)
                return base.Attach(entity);
            return CreateGenericDbContext(entity).Attach(entity);
        }

        public override void AttachRange(params object[] entities)
        {
            if (IsExecutor)
            {
                base.AttachRange(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                 aggregateKv.Key.AttachRange(aggregateKv.Value);
            }
        }

        public override void AttachRange(IEnumerable<object> entities)
        {
            if (IsExecutor)
            {
                base.AttachRange(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.AttachRange(aggregateKv.Value);
            }
        }

        public override EntityEntry<TEntity> Entry<TEntity>(TEntity entity)
        {
            if (IsExecutor)
                return base.Entry(entity);
            return CreateGenericDbContext(entity).Entry(entity);
        }

        public override EntityEntry Entry(object entity)
        {
            if (IsExecutor)
                return base.Entry(entity);
            return CreateGenericDbContext(entity).Entry(entity);
        }

        public override EntityEntry<TEntity> Update<TEntity>(TEntity entity)
        {
            if (IsExecutor)
                return base.Update(entity);
            return CreateGenericDbContext(entity).Update(entity);
        }

        public override EntityEntry Update(object entity)
        {
            if (IsExecutor)
                return base.Update(entity);
            return CreateGenericDbContext(entity).Update(entity);
        }

        public override void UpdateRange(params object[] entities)
        {
            if (IsExecutor)
            {
                base.UpdateRange(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.UpdateRange(aggregateKv.Value);
            }
        }

        public override void UpdateRange(IEnumerable<object> entities)
        {
            if (IsExecutor)
            {
                base.UpdateRange(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.UpdateRange(aggregateKv.Value);
            }
        }

        public override EntityEntry<TEntity> Remove<TEntity>(TEntity entity)
        {
            if (IsExecutor)
                return base.Remove(entity);
            return CreateGenericDbContext(entity).Remove(entity);
        }

        public override EntityEntry Remove(object entity)
        {
            if (IsExecutor)
                return base.Remove(entity);
            return CreateGenericDbContext(entity).Remove(entity);
        }

        public override void RemoveRange(params object[] entities)
        {
            if (IsExecutor)
            {
                base.RemoveRange(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.RemoveRange(aggregateKv.Value);
            }
        }

        public override void RemoveRange(IEnumerable<object> entities)
        {
            if (IsExecutor)
            {
                base.RemoveRange(entities);
                return;
            }
            var aggregateToDic = AggregateToDic(entities);
            foreach (var aggregateKv in aggregateToDic)
            {
                aggregateKv.Key.RemoveRange(aggregateKv.Value);
            }
        }

        public override int SaveChanges()
        {

            if (IsExecutor)
                return base.SaveChanges();
            return this.SaveChanges(true);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            if (IsExecutor)
                return base.SaveChanges(acceptAllChangesOnSuccess);
            //ApplyShardingConcepts();
            int i = 0;
            //如果是内部开的事务就内部自己消化
            if (Database.AutoTransactionsEnabled&&Database.CurrentTransaction==null&&ShardingDbContextExecutor.IsMultiDbContext)
            {
                using (var tran = Database.BeginTransaction())
                {
                    i = ShardingDbContextExecutor.SaveChanges(acceptAllChangesOnSuccess);
                    tran.Commit();
                }
            }
            else
            {
                i = ShardingDbContextExecutor.SaveChanges(acceptAllChangesOnSuccess);
            }

            return i;
        }


        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            if (IsExecutor)
                return base.SaveChangesAsync(cancellationToken);
            return this.SaveChangesAsync(true, cancellationToken);
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = new CancellationToken())
        {
            if (IsExecutor)
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess,cancellationToken);
            //ApplyShardingConcepts();
            int i = 0;
            //如果是内部开的事务就内部自己消化
            if (Database.AutoTransactionsEnabled && Database.CurrentTransaction==null && ShardingDbContextExecutor.IsMultiDbContext)
            {
                using (var tran = await Database.BeginTransactionAsync(cancellationToken))
                {
                    i = await ShardingDbContextExecutor.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
                    await tran.CommitAsync(cancellationToken);
                }
            }
            else
            {
                i = await ShardingDbContextExecutor.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }


            return i;
        }

        public override void Dispose()
        {

            if (IsExecutor)
            {
                base.Dispose();
            }
            else
            {
                ShardingDbContextExecutor.Dispose();
                base.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            if (IsExecutor)
            {
                await base.DisposeAsync();
            }
            else
            {
                await ShardingDbContextExecutor.DisposeAsync();

                await base.DisposeAsync();
            }
        }
        public Task RollbackAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return ShardingDbContextExecutor.RollbackAsync(cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken = new CancellationToken())
        {
            return ShardingDbContextExecutor.CommitAsync(cancellationToken);
        }

        public void NotifyShardingTransaction()
        {
            ShardingDbContextExecutor.NotifyShardingTransaction();
        }

        public void Rollback()
        {
            ShardingDbContextExecutor.Rollback();
        }

        public void Commit()
        {
            ShardingDbContextExecutor.Commit();
        }

        //public IDictionary<string, IDataSourceDbContext> GetCurrentDbContexts()
        //{
        //    return ShardingDbContextExecutor.GetCurrentDbContexts();
        //}
        
    }
}