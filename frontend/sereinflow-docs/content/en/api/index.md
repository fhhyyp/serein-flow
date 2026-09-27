# REST API reference

The API lets your program interact with SereinFlow. Draw a flow in the console, then read it, start it, and inspect results through HTTP. Publish it and create a public interface for stable external access.

For canvas nodes, see [Node libraries](/en/library/). To run the service inside ASP.NET Core, see [Host integration](/en/integration/hosting).

## First call

1. Read [URLs, keys, and errors](/en/api/basics) to confirm API access.
2. Find project and flow IDs through [Projects and flows API](/en/api/projects).
3. Start and inspect a run through [Runs API](/en/api/runs).
4. For external callers, read [Flow versions](/en/api/versions) and [Public interfaces](/en/api/public-interfaces).

## Find endpoints by task

| Task | Reference |
| --- | --- |
| Find projects and save flows | [Projects and flows](/en/api/projects) |
| Publish or roll back a flow | [Flow versions](/en/api/versions) |
| Start runs and read outputs or files | [Runs, events, and workpieces](/en/api/runs) |
| Upload or upgrade node libraries | [Node libraries](/en/api/libraries) |
| Invoke published flows externally | [Public interfaces](/en/api/public-interfaces) |
| Manage runtime settings and keys | [Environment and keys](/en/api/environment) |
| Pause and step through a flow | [Debug sessions](/en/api/debug) |

This reference lists routes in the current code. The running API's Swagger UI (default `http://localhost:8188/swagger`) shows full field models and is enabled by default in Development.
