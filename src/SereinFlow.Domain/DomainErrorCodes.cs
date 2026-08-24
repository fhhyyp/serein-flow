namespace SereinFlow.Domain;

public static class DomainErrorCodes
{
    public const string EmptyName = "domain.empty_name";
    public const string DuplicateCanvasId = "flow.duplicate_canvas_id";
    public const string DuplicateNodeId = "flow.duplicate_node_id";
    public const string UnknownEntryNode = "flow.unknown_entry_node";
    public const string UnknownConnectionEndpoint = "flow.unknown_connection_endpoint";
    public const string DuplicateConnectionId = "flow.duplicate_connection_id";
    public const string DuplicateParameterName = "node.duplicate_parameter_name";
    public const string MissingRequiredParameter = "node.missing_required_parameter";
    public const string InvalidSourceHash = "script.invalid_source_hash";
}
