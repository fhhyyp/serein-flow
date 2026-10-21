using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SereinFlow.Mcp.Framework;

/// <summary>Loads a generated registry. This class never scans runtime assemblies or methods.</summary>
public static class McpToolDiscovery
{
    public static IServiceCollection AddMcpToolHandlers(this IServiceCollection services, IMcpToolRegistry registry, params Type[] toolTypes)
    {
        services.TryAddSingleton<McpToolExecutor>();
        services.TryAddSingleton<McpToolMutationGate>();
        foreach (var tool in SelectTools(registry, toolTypes)) tool.RegisterHandler(services);
        return services;
    }

    public static McpToolCatalog CreateCatalog(IMcpToolRegistry registry,
        IReadOnlyList<McpToolDescriptor>? contracts = null, McpToolDiscoveryOptions? options = null,
        IEnumerable<Type>? toolTypes = null)
    {
        options ??= new McpToolDiscoveryOptions();
        var jsonOptions = new JsonSerializerOptions(options.JsonOptions);
        jsonOptions.MakeReadOnly(populateMissingResolver: true);
        var descriptors = contracts?.ToDictionary(item => item.Name, StringComparer.Ordinal)
            ?? new Dictionary<string, McpToolDescriptor>(StringComparer.Ordinal);
        var tools = SelectTools(registry, toolTypes).Select(tool => CreateTool(tool, descriptors, jsonOptions)).ToArray();
        var catalog = new McpToolCatalog(tools);
        if (contracts is null) return catalog;
        var byName = tools.ToDictionary(tool => tool.Descriptor.Name, StringComparer.Ordinal);
        foreach (var contract in contracts)
        {
            if (!byName.ContainsKey(contract.Name))
                throw new InvalidOperationException($"MCP contract '{contract.Name}' has no generated tool method.");
        }
        return new McpToolCatalog(contracts.Select(contract => byName[contract.Name])
            .Concat(tools.Where(tool => !descriptors.ContainsKey(tool.Descriptor.Name))));
    }

    private static IEnumerable<McpGeneratedTool> SelectTools(IMcpToolRegistry registry, IEnumerable<Type>? toolTypes)
    {
        var selected = toolTypes?.ToHashSet();
        return selected is null || selected.Count == 0 ? registry.Tools : registry.Tools.Where(tool => selected.Contains(tool.HandlerType));
    }

    private static McpToolDefinition CreateTool(McpGeneratedTool tool,
        IReadOnlyDictionary<string, McpToolDescriptor> contracts, JsonSerializerOptions jsonOptions)
    {
        if (!contracts.TryGetValue(tool.Metadata.Name, out var descriptor))
        {
            if (tool.RequestType == typeof(JsonElement) || string.IsNullOrWhiteSpace(tool.Metadata.Description))
                throw new InvalidOperationException($"Tool '{tool.Metadata.Name}' requires a description and a typed request, or an explicit contract.");
            var schemaNode = tool.RequestType is null ? null : jsonOptions.GetJsonSchemaAsNode(tool.RequestType);
            if (schemaNode is not null) schemaNode["type"] = "object";
            if (schemaNode is not null && tool.RequiredProperties is not null)
            {
                var required = (schemaNode["required"] as JsonArray)?.Select(item => item!.GetValue<string>()) ?? [];
                schemaNode["required"] = JsonSerializer.SerializeToNode(required.Concat(tool.RequiredProperties(jsonOptions)).Distinct(StringComparer.Ordinal).ToArray());
            }
            var schema = schemaNode is null
                ? JsonSerializer.SerializeToElement(new { type = "object", properties = new { } })
                : JsonSerializer.SerializeToElement(schemaNode);
            descriptor = new McpToolDescriptor(tool.Metadata.Name, tool.Metadata.Description, schema);
        }
        return new McpToolDefinition(descriptor, tool.Metadata,
            (context, arguments, cancellationToken) => tool.Execute(context, arguments, jsonOptions, cancellationToken));
    }
}
