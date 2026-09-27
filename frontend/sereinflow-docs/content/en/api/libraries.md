# Node libraries API

A library is a package of reusable .NET nodes. Upload it to the server, then reference it from a project.

## Browse and upload

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/node-catalog/builtins` | Built-in nodes |
| `GET` | `/api/libraries` | Libraries |
| `GET` | `/api/libraries/usage` | Library usage |
| `GET` | `/api/libraries/{libraryId}` | One library |
| `POST` | `/api/libraries/upload` | Upload ZIP as `multipart/form-data` |
| `POST` | `/api/libraries/upload-zip` | Alternate ZIP upload |
| `PATCH` | `/api/libraries/{libraryId}/family` | Assign family |
| `PATCH` | `/api/libraries/{libraryId}/lifecycle` | Change lifecycle state |
| `DELETE` | `/api/libraries/{libraryId}` | Archive library |
| `GET` | `/api/library-families` | List families |
| `GET` | `/api/library-families/{familyId}/artifacts` | Family artifacts |

The uploaded form field must be named `file`; otherwise the server returns `400`.

## Project references

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/api/projects/{projectId}/libraries` | Project references |
| `GET` | `/api/projects/{projectId}/libraries/usage` | Usage in this project |
| `PUT` | `/api/projects/{projectId}/libraries/{libraryId}` | Add to project |
| `DELETE` | `/api/projects/{projectId}/libraries/{libraryId}` | Remove from project |

A successful upload does not add a project reference. Check it if nodes are absent.

## Upgrades

First call `POST /api/projects/{projectId}/library-upgrades/preview`, then inspect `GET /api/projects/{projectId}/library-upgrades/{upgradeId}`. After reviewing affected flows, use `/apply` or `/apply-batch` under the same path.

Environment administrators can list archived libraries through `/api/environment/libraries`, upload through `/api/environment/libraries/upload`, and manage a library through `/archive` or `/reindex`.

See [Nodes and libraries](/en/modules/nodes-and-libraries) for the concepts.
