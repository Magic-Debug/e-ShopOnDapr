using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.eShopOnDapr.BuildingBlocks.EventBus;
using Microsoft.eShopOnDapr.BuildingBlocks.EventBus.Abstractions;
using Microsoft.eShopOnDapr.Services.Ordering.API.Actors;
using Microsoft.eShopOnDapr.Services.Ordering.API.Controllers;
using Microsoft.eShopOnDapr.Services.Ordering.API.Infrastructure;
using Microsoft.eShopOnDapr.Services.Ordering.API.Infrastructure.Filters;
using Microsoft.eShopOnDapr.Services.Ordering.API.Infrastructure.Repositories;
using Microsoft.eShopOnDapr.Services.Ordering.API.Infrastructure.Services;
using Microsoft.eShopOnDapr.Services.Ordering.API.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

namespace Microsoft.eShopOnDapr.Services.Ordering.API
{

    /// <summary>
    /// 注册中心，服务发现，服务调用链路追踪，请求熔断，重试限流
    /// </summary>
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers().AddDapr();//将DaprClient类添加到 ASP.NET Core注入系统

            services.AddCustomSwagger(Configuration);
            services.AddCustomAuth(Configuration);
            services.AddCustomHealthChecks(Configuration);

            services.AddActors(options =>
            {
                options.Actors.RegisterActor<OrderingProcessActor>();//添加Actor
            });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .SetIsOriginAllowed((host) => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            services.AddScoped<IEventBus, DaprEventBus>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IIdentityService, IdentityService>();
            services.AddScoped<IEmailService, EmailService>();

            services.Configure<OrderingSettings>(Configuration);

            services.AddSignalR();

            services.AddDbContext<OrderingDbContext>(
                options => options.UseMySQL(Configuration["MySqlConnectionString"]));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Ordering.API V1");
                    c.OAuthClientId("orderingswaggerui");
                    c.OAuthAppName("Ordering Swagger UI");
                });
            }

            app.UseRouting();
            //添加发布支持
            app.UseCloudEvents();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseCors("CorsPolicy");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
                endpoints.MapControllers();
                //添加订阅支持
                endpoints.MapSubscribeHandler();

                endpoints.MapActorsHandlers();

                endpoints.MapHealthChecks("/hc", new HealthCheckOptions()
                {
                    Predicate = _ => true,
                    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
                });
                endpoints.MapHealthChecks("/liveness", new HealthCheckOptions
                {
                    Predicate = r => r.Name.Contains("self")
                });
                endpoints.MapHub<NotificationsHub>("/hub/notificationhub",
                    options => options.Transports = AspNetCore.Http.Connections.HttpTransportType.LongPolling);
            });
        }
    }

    static class CustomServiceExtensions
    {
        public static IServiceCollection AddCustomSwagger(this IServiceCollection services, IConfiguration configuration)
            => services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "eShopOnDapr - Ordering API", Version = "v1" });

                var identityUrlExternal = configuration.GetValue<string>("IdentityUrlExternal");

                c.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows()
                    {
                        Implicit = new OpenApiOAuthFlow()
                        {
                            AuthorizationUrl = new Uri($"{identityUrlExternal}/connect/authorize"),
                            TokenUrl = new Uri($"{identityUrlExternal}/connect/token"),
                            Scopes = new Dictionary<string, string>()
                            {
                                { "ordering", "Ordering API" }
                            }
                        }
                    }
                });

                c.OperationFilter<AuthorizeCheckOperationFilter>();
            });

        public static IServiceCollection AddCustomAuth(this IServiceCollection services, IConfiguration configuration)
        {
            // Prevent mapping "sub" claim to nameidentifier.
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

            services.AddAuthentication("Bearer")
                .AddJwtBearer(options =>
                {
                    options.Audience = "ordering-api";
                    options.Authority = configuration.GetValue<string>("IdentityUrl");
                    options.RequireHttpsMetadata = false;
                });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("ApiScope", policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", "ordering");
                });
            });

            return services;
        }

        public static IServiceCollection AddCustomHealthChecks(this IServiceCollection services, IConfiguration configuration)
        {
            var builder = services.AddHealthChecks();
            builder
                .AddCheck("self", () => HealthCheckResult.Healthy())
                .AddDapr()
                .AddMySql(
                    configuration["SqlConnectionString"],
                    name: "OrderingDB-check",
                    tags: new string[] { "orderingdb" });

            return services;
        }

    }
}
