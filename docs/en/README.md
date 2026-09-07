# SereinFlow Documentation

[Chinese documentation index](../zh-CN/README.md)

This directory contains maintained reference material only. Historical design
notes, implementation plans, migration records, and risk reports are kept out
of the published documentation so that operators and contributors can quickly
find the current material.

## References

- [MCP service reference](mcp-readonly-server.md): HTTP and stdio transports,
  authentication, resources, prompts, tools, and operational boundaries.
- [Worker protocol](worker-protocol.md): the current v2 production protocol,
  v1 historical compatibility differences, JSON Lines boundaries, session flow,
  message bridging, and stable error codes.
- [Worker message service](worker-message-service.md): the run-scoped
  queue, event bus, DI, and controlled external-ingress behavior.
- [.NET client SDK](client-sdk.md): invoke flows over HTTP, inspect runs,
  stream events, publish messages, and download workpieces.
- [Node library development guide](node-library-development.md): the
  `SereinFlow.Library` SDK, node metadata, inputs/outputs, injection, messaging,
  workpieces, native dependencies, the OpenCV example, packaging, and upgrades.

For first-time local setup and the recommended MCP client workflow, start with
the repository [README](../../README.md).
