using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using Hangfire;
using Hangfire.MemoryStorage;
using CompVis_StableDiffusion_Api.Services;
using Raven.Client.Documents;
using Audit.Core;
using System.Text;
using System.Reflection;
using System.IO;
using Microsoft.AspNetCore.HttpOverrides;

namespace CompVis_StableDiffusion_Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
            //services.AddRazorPages();

            services.AddSwaggerGen(c =>
            {
                c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
            });

            var settings = new Settings();

            services.AddSingleton(settings);
            services.AddScoped<ILogService, LogService>();
            services.AddScoped<IShellService, ShellService>();
            //services.AddScoped<IShellService, FakeShellService>();
            services.AddScoped<IStorageService, StorageService>();
            services.AddScoped<ITextToImageService, TextToImageService>();

            var store = new DocumentStore
            {
                Urls = new[] { settings.StorageConnectionString },
                Database = settings.StorageDatabase
            };
            store.Initialize();
            services.AddSingleton<IDocumentStore>(store);

            System.IO.Directory.CreateDirectory(settings.OutputDir);

            services.AddHangfire(c => c
                .UseMemoryStorage());
            
            services.AddHangfireServer(opt => 
            { 
                opt.WorkerCount = settings.WorkerCount; 
            });

            services.AddControllers();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHangfireDashboard();

            app.UseHttpsRedirection();
            app.UseStaticFiles();
                        
            app.UseRouting();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseAuthorization();
            app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            app.UseEndpoints(endpoints =>
            {
                //endpoints.MapRazorPages();
                endpoints.MapControllers();
                endpoints.MapHangfireDashboard();
            });

            Audit.Core.Configuration.Setup()
                .UseUdp(udp => udp
                    .RemoteAddress("127.0.0.1")
                    .RemotePort(2223)
                    .CustomSerializer(ev =>
                    {
                        if (ev.EventType == "Ephemeral")
                        {
                            return Encoding.UTF8.GetBytes(ev.CustomFields["Status"] as string);
                        }
                        else if (ev is Audit.WebApi.AuditEventWebApi)
                        {
                            var action = (ev as Audit.WebApi.AuditEventWebApi)!.Action;
                            var msg = $"Action: {action.ControllerName}/{action.ActionName}{new Uri(action.RequestUrl).Query} - Response: {action.ResponseStatusCode} {action.ResponseStatus}. Event: {action.ToJson()}";
                            return Encoding.UTF8.GetBytes(msg);
                        }
                        return new byte[0];
                    }));
        }
    }
}
