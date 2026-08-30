using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace SereinFlow.Api;

internal static class SereinFlowWebApiExtensions
{
    public static IServiceCollection AddSereinFlowWebApi(this IServiceCollection services)
    {
        services.ConfigureHttpJsonOptions(options => ConfigureJson(options.SerializerOptions));
        services.AddControllers(options =>
            options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
            .AddJsonOptions(options => ConfigureJson(options.JsonSerializerOptions));
        services.Configure<ApiBehaviorOptions>(options =>
            options.SuppressModelStateInvalidFilter = true);
        return services;
    }

    public static IServiceCollection AddSereinFlowApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
            options.SwaggerDoc("v1", new()
            {
                Title = "SereinFlow API",
                Version = "v1",
                Description = "SereinFlow REST API. MCP JSON-RPC and SignalR transports are documented separately.",
            }));
        return services;
    }

    public static WebApplication UseSereinFlowApiDocumentation(this WebApplication app)
    {
        var enabled = app.Configuration.GetValue<bool?>("SereinFlow:ApiDocumentation:Enabled")
            ?? app.Environment.IsDevelopment();
        if (!enabled)
            return app;

        app.UseSwagger(options => options.RouteTemplate = "openapi/{documentName}.json");
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.SwaggerEndpoint("/openapi/v1.json", "SereinFlow API v1");
        });
        return app;
    }

    private static void ConfigureJson(JsonSerializerOptions options)
    {
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }
}
