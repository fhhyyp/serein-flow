using System.ComponentModel.DataAnnotations;

namespace SereinFlow.Mcp.Tools;

internal sealed record RevokeMcpApiKeyRequest([property: Required] string KeyId, [property: Required] string IdempotencyKey);
