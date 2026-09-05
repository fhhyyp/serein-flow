# SereinFlow Documentation

[Chinese documentation index](../zh-CN/README.md)

This directory contains maintained reference material only. Historical design
notes, implementation plans, migration records, and risk reports are kept out
of the published documentation so that operators and contributors can quickly
find the current material.

## References

- [MCP service reference](mcp-readonly-server.md): HTTP and stdio transports,
  authentication, resources, prompts, tools, and operational boundaries.
- [Worker protocol v2](worker-protocol-v2.md): the versioned JSON Lines
  contract between the API supervisor and disposable Worker Runner processes,
  including message delivery control frames.
- [Worker protocol v1 (historical)](worker-protocol-v1.md): the historical v1
  JSON Lines contract and its stable error codes.
- [Worker message service](worker-message-service.md): the run-scoped
  queue, event bus, DI, and controlled external-ingress behavior.

For first-time local setup and the recommended MCP client workflow, start with
the repository [README](../../README.md).
