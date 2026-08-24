namespace SereinFlow.Domain;

public sealed class FlowDefinition
{
    private FlowDefinition(
        Guid id,
        int schemaVersion,
        long version,
        IReadOnlyList<CanvasDefinition> canvases,
        string entryNodeId,
        string checksum)
    {
        Id = id;
        SchemaVersion = schemaVersion;
        Version = version;
        Canvases = canvases;
        EntryNodeId = entryNodeId;
        Checksum = checksum;
    }

    public Guid Id { get; }

    public int SchemaVersion { get; }

    public long Version { get; }

    public IReadOnlyList<CanvasDefinition> Canvases { get; }

    public string EntryNodeId { get; }

    public string Checksum { get; }

    public static FlowDefinition Create(
        Guid id,
        long version,
        IEnumerable<CanvasDefinition> canvases,
        string entryNodeId,
        int schemaVersion = 1,
        string checksum = "")
    {
        ArgumentNullException.ThrowIfNull(canvases);
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Flow ID cannot be empty.", nameof(id));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), "Flow version must be positive.");
        }

        if (schemaVersion < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Schema version must be positive.");
        }

        // An empty entry node is a valid editor draft. Execution validates this separately.
        return new FlowDefinition(id, schemaVersion, version, canvases.ToArray(), entryNodeId?.Trim() ?? string.Empty, checksum ?? string.Empty);
    }

    public IReadOnlyList<DomainDiagnostic> Validate()
    {
        var diagnostics = new List<DomainDiagnostic>();
        var canvasIds = new HashSet<string>(StringComparer.Ordinal);
        var nodeIds = new HashSet<string>(StringComparer.Ordinal);
        var connectionIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var canvas in Canvases)
        {
            if (!canvasIds.Add(canvas.Id))
            {
                diagnostics.Add(new(DomainErrorCodes.DuplicateCanvasId, $"Canvas '{canvas.Id}' is duplicated.", $"canvases.{canvas.Id}"));
            }

            foreach (var node in canvas.Nodes)
            {
                if (!nodeIds.Add(node.Id))
                {
                    diagnostics.Add(new(DomainErrorCodes.DuplicateNodeId, $"Node '{node.Id}' is duplicated.", $"nodes.{node.Id}"));
                }

                var parameterNames = new HashSet<string>(StringComparer.Ordinal);
                foreach (var parameter in node.Parameters)
                {
                    if (!parameterNames.Add(parameter.Name))
                    {
                        diagnostics.Add(new(
                            DomainErrorCodes.DuplicateParameterName,
                            $"Parameter '{parameter.Name}' is duplicated on node '{node.Id}'.",
                            $"nodes.{node.Id}.parameters.{parameter.Name}"));
                    }

                    if (parameter.Required && parameter.Source == DataSource.Literal && string.IsNullOrWhiteSpace(parameter.ValueJson))
                    {
                        diagnostics.Add(new(
                            DomainErrorCodes.MissingRequiredParameter,
                            $"Required parameter '{parameter.Name}' on node '{node.Id}' has no literal value.",
                            $"nodes.{node.Id}.parameters.{parameter.Name}"));
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(EntryNodeId) && !nodeIds.Contains(EntryNodeId))
        {
            diagnostics.Add(new(DomainErrorCodes.UnknownEntryNode, $"Entry node '{EntryNodeId}' does not exist.", "entryNodeId"));
        }

        foreach (var canvas in Canvases)
        {
            foreach (var connection in canvas.Connections)
            {
                if (!connectionIds.Add(connection.Id))
                {
                    diagnostics.Add(new(DomainErrorCodes.DuplicateConnectionId, $"Connection '{connection.Id}' is duplicated.", $"connections.{connection.Id}"));
                }

                if (!nodeIds.Contains(connection.FromNodeId) || !nodeIds.Contains(connection.ToNodeId))
                {
                    diagnostics.Add(new(
                        DomainErrorCodes.UnknownConnectionEndpoint,
                        $"Connection '{connection.Id}' references an unknown node.",
                        $"connections.{connection.Id}"));
                }
            }
        }

        return diagnostics;
    }
}
