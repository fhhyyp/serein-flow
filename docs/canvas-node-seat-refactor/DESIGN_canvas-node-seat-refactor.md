# Design: Canvas Node Seat Refactor

## Reference mapping

```mermaid
flowchart LR
  R[TRAE reference client] --> S[Explicit connector seats]
  S --> I[Execution input/output]
  S --> P[Parameter input seats]
  S --> O[Data output seat]
  I --> V[Vue Flow Handle]
  P --> V
  O --> V
  V --> D[Persisted ConnectionDto]
```

The reference client models connectors separately from the node body. The new implementation keeps that principle but uses Vue Flow's managed handles so edge geometry, viewport transforms, selection, and keyboard deletion remain available.

## Seat contract

`src/flow/connectionSeats.ts` is the single source of truth for the seat list and vertical layout:

| Seat | Side | Direction | Semantic | Multiplicity |
| --- | --- | --- | --- | --- |
| `exec-in` | left | target | execution | one |
| `param-{id}` | left | target | data | one |
| `exec-out` | right | source | execution | many |
| `data-out` | right | source | data | many |

The node card renders this model rather than manually positioning individual handles. The seat ID is also the persisted port ID, so visual changes do not rewrite connections.

## User flow

```mermaid
sequenceDiagram
  participant Library as Node library
  participant Canvas as Active canvas
  participant Flow as Vue Flow
  participant API as Project API
  Library->>Canvas: click or drag node definition
  Canvas->>Flow: create node at snapped flow coordinate
  Flow->>Canvas: connect(source seat, target seat)
  Canvas->>Canvas: update parameter source for data links
  Canvas->>API: save complete project flow
  API-->>Canvas: versioned definition
```

## Project and canvas boundaries

- The project picker owns server project selection and new blank-project drafts.
- The canvas tab row owns lifecycle canvas selection and optional lifecycle canvas creation.
- The node card owns only node presentation and seat rendering; persistence and history remain in `App.vue`/workspace state.

## Visual constraints

- Keep white surfaces, slate text, thin borders, compact typography, blue execution accents, and purple data accents.
- Avoid gradient node cards, emoji connector icons, large onboarding artwork, or a separate visual language for the seat model.
- Use focus-visible outlines and restrained 160–180ms transitions.

