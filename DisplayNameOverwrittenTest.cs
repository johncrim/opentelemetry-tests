using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Trace;
using Xunit;

public class DisplayNameOverwrittenTest
{

    /// <summary>
    /// Test for https://github.com/open-telemetry/opentelemetry-dotnet-contrib/issues/1948
    /// </summary>
    [Fact]
    public async Task ActivityDisplayName_CanBeSetInHandler()
    {
        var recordedActivities = new List<Activity>();

        var builder = new HostBuilder()
           .ConfigureWebHost(webBuilder =>
                             {
                                 webBuilder
                                    .UseTestServer()
                                    .ConfigureServices(services =>
                                                       {
                                                           services.AddOpenTelemetry()
                                                                   .WithTracing(builder =>
                                                                                {
                                                                                    builder.AddAspNetCoreInstrumentation();
                                                                                    builder.AddInMemoryExporter(recordedActivities);
                                                                                });
                                                           services.AddRouting();
                                                       })
                                    .Configure(app =>
                                               {
                                                   app.UseRouting();
                                                   app.UseEndpoints(SetupEndpoints);
                                               });
                             });

        void SetupEndpoints(IEndpointRouteBuilder routeBuilder)
        {
            routeBuilder.Map("{**path}",
                             context =>
                             {
                                 // Here I'm setting the Activity.DisplayName to something more useful for this specific route
                                 var activity = context.Features.Get<IHttpActivityFeature>()?.Activity;
                                 if (activity?.IsAllDataRequested == true)
                                 {
                                     var request = context.Request;
                                     activity.DisplayName = $"{request.Method.ToUpperInvariant()} {request.Path}";
                                 }

                                 context.Response.StatusCode = 200;
                                 return Task.CompletedTask;
                             });
        }

        using var host = await builder.StartAsync();
        using var client = host.GetTestClient();
        var response = await client.GetAsync("/foo/bar");

        var requestActivity = Assert.Single(recordedActivities);
        Assert.Equal("GET /foo/bar", requestActivity.DisplayName);
    }

}