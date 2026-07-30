# Generated API types

`schema.d.ts` in this folder is **generated**. Do not edit it by hand — any
change is overwritten by the next generation run.

## Where it comes from

```
api/src/Q2.Api/**          the endpoints and DTOs
  -> api/openapi/q2-api.json    exported OpenAPI 3.0 contract (committed)
    -> app/app/api/generated/schema.d.ts   TypeScript types (committed)
```

## Regenerating

After changing any endpoint, DTO or enum in the backend:

```bash
bun run api:openapi
```

That exports the OpenAPI document from the running application's routes and
regenerates this file. To regenerate only the types from the committed
contract:

```bash
bun run api:types
```

Both artefacts are committed, so a frontend-only checkout type-checks without a
.NET toolchain, and CI fails if either is out of date.

## Using them

Never import from this file directly in a component. Go through `~/api/types`
and the functions in `~/api/goals`, which is where the naming and the error
handling live.
