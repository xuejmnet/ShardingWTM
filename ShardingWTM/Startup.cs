using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShardingCore;
using ShardingCore.Bootstrappers;
using ShardingCore.Core.DbContextCreator;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources;
using ShardingCore.Core.VirtualDatabase.VirtualDataSources.Abstractions;
using ShardingCore.TableExists;
using ShardingWTM.EFCore;
using ShardingWTM.EFCore.Sharding;
using WalkingTec.Mvvm.Core;
using WalkingTec.Mvvm.Core.Extensions;
using WalkingTec.Mvvm.Core.Support.FileHandlers;
using WalkingTec.Mvvm.Mvc;

namespace ShardingWTM
{
    public class Startup
    {
        public static readonly ILoggerFactory efLogger = LoggerFactory.Create(builder =>
        {
            builder.AddFilter((category, level) => category == DbLoggerCategory.Database.Command.Name && level == LogLevel.Information).AddConsole();
        });
        public IConfiguration ConfigRoot { get; }

        public Startup(IWebHostEnvironment env, IConfiguration config)
        {
            ConfigRoot = config;
        }


        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDistributedMemoryCache();
            services.AddWtmSession(3600, ConfigRoot);
            services.AddWtmCrossDomain(ConfigRoot);
            services.AddWtmAuthentication(ConfigRoot);
            services.AddWtmHttpClient(ConfigRoot);
            services.AddWtmSwagger();
            services.AddWtmMultiLanguages(ConfigRoot);

            services.AddMvc(options =>
            {
                options.UseWtmMvcOptions();
            })
            .AddJsonOptions(options => {
                options.UseWtmJsonOptions();
            })
            
            .ConfigureApiBehaviorOptions(options =>
            {
                options.UseWtmApiOptions();
            })
            .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix)
            .AddWtmDataAnnotationsLocalization(typeof(Program));
            
            services.AddWtmContext(ConfigRoot, (options)=> {
                options.DataPrivileges = DataPrivilegeSettings();
                options.CsSelector = CSSelector;
                options.FileSubDirSelector = SubDirSelector;
                options.ReloadUserFunc = ReloadUser;
            });
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
                    o.CreateDataBaseOnlyOnStart = true;
                    //o.CreateShardingTableOnStart = true;
                    //o.EnsureCreatedWithOutShardingTable = true;
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
                        build.UseMySql(conn, new MySqlServerVersion(new Version())).UseLoggerFactory(efLogger);
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
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IOptionsMonitor<Configs> configs)
        {
            IconFontsHelper.GenerateIconFont();
            // using (var scope = app.ApplicationServices.CreateScope())
            // {
            //     var requiredService = scope.ServiceProvider.GetRequiredService<WTMContext>();
            //     var requiredServiceDc = requiredService.DC;
            // }
            //定时任务
            app.ApplicationServices.UseAutoShardingCreate();

            using (var scope=app.ApplicationServices.CreateScope())
            {
                var dbconContext = scope.ServiceProvider.GetRequiredService<DataContext>();
                dbconContext.Database.Migrate();
            }
            //补齐表防止iis之类的休眠导致按天按月的表没有新建
            app.ApplicationServices.UseAutoTryCompensateTable();
            app.UseExceptionHandler(configs.CurrentValue.ErrorHandler);
            app.UseStaticFiles();
            app.UseWtmStaticFiles();
            app.UseRouting();
            app.UseWtmMultiLanguages();
            app.UseWtmCrossDomain();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseSession();
            app.UseWtmSwagger();
            app.UseWtm();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                   name: "areaRoute",
                   pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });

            app.UseWtmContext();
        }

        /// <summary>
        /// Wtm will call this function to dynamiclly set connection string
        /// 框架会调用这个函数来动态设定每次访问需要链接的数据库
        /// </summary>
        /// <param name="context">ActionContext</param>
        /// <returns>Connection string key name</returns>
        public string CSSelector(ActionExecutingContext context)
        {
            //To override the default logic of choosing connection string,
            //change this function to return different connection string key
            //根据context返回不同的连接字符串的名称
            return null;
        }

        /// <summary>
        /// Set data privileges that system supports
        /// 设置系统支持的数据权限
        /// </summary>
        /// <returns>data privileges list</returns>
        public List<IDataPrivilege> DataPrivilegeSettings()
        {
            List<IDataPrivilege> pris = new List<IDataPrivilege>();
            //Add data privilege to specific type
            //指定哪些模型需要数据权限
            return pris;
        }

        /// <summary>
        /// Set sub directory of uploaded files
        /// 动态设置上传文件的子目录
        /// </summary>
        /// <param name="fh">IWtmFileHandler</param>
        /// <returns>subdir name</returns>
        public string SubDirSelector(IWtmFileHandler fh)
        {
            return null;
        }

        /// <summary>
        /// Custom Reload user process when cache is not available
        /// 设置自定义的方法重新读取用户信息，这个方法会在用户缓存失效的时候调用
        /// </summary>
        /// <param name="context"></param>
        /// <param name="account"></param>
        /// <returns></returns>
        public LoginUserInfo ReloadUser(WTMContext context, string account)
        {
            return null;
        }
    }
}
