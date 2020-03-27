using System.Reflection;
using Datadog.RuntimeMetrics;
using Datadog.RuntimeMetrics.Hosting;
using Datadog.Trace;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AspNetCore31
{
    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            // register the global Tracer
            services.AddDatadogTracing();

            // register the services required to collect metrics and send them to dogstatsd
            services.AddDatadogRuntimeMetrics();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, Tracer tracer, IOptions<TracingOptions> tracingOptions)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            if (tracingOptions.Value.Diagnostic_Source_Enabled)
            {
                // hack: internal method
                tracer.GetType()
                      .GetMethod("StartDiagnosticObservers", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                     ?.Invoke(tracer, null);
            }

            if (tracingOptions.Value.Middleware_Enabled)
            {
                // if enabled, create a span for each request using middleware
                app.UseDatadogTracing(tracer);
            }

            app.Run(async context =>
                    {
                        using (Scope? scope = tracingOptions.Value.Manual_Spans_Enabled ? tracer.StartActive("manual") : null)
                        {
                            if (scope != null)
                            {
                                Span span = scope.Span;
                                span.Type = SpanTypes.Custom;
                                span.SetTag("tag1", "value1");
                            }

                            if (tracingOptions.Value.Manual_Spans_Enabled)
                            {
                                for (int i = 0; i < 5; i++)
                                {
                                    using (Scope innerScope = tracer.StartActive("manual"))
                                    {
                                        Span span = innerScope.Span;
                                        span.Type = SpanTypes.Custom;
                                    }
                                }
                            }

                            await context.Response.WriteAsync("Hello, world!");
                        }
                    });
        }
    }
}