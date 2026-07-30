# 0003 — Feature-organised minimal APIs, no repository layer

- **Status:** Accepted
- **Date:** 2026-07-29

## Context

The backend must be modular and understandable without becoming an
architecture exercise. The brief explicitly warns against overblown Clean
Architecture, empty layer projects and generic repository abstractions.

The default .NET template for "serious" projects is four assemblies — Domain,
Application, Infrastructure, Api — plus `IRepository<T>`, a unit-of-work
wrapper and a mediator. For one entity and three endpoints, that is a lot of
structure holding very little.

## Decision

**One project, organised by feature.**

```
Features/Goals/          Goal, GoalParticipant, GoalStatus, contracts,
                         validator, service, EF configuration, endpoints
Features/Diagnostics/    health check and deliberate-failure endpoints
Infrastructure/          persistence, errors, observability, time, CLI
```

A feature owns its whole vertical. Adding one means adding a folder, not
editing four projects.

Specifically **not** used:

- **No repository or unit-of-work abstraction over EF Core.** `DbContext` is
  already both. Wrapping it adds a layer with no behaviour of its own, makes
  `Include`, projections and `ExecuteDelete` awkward, and does not make testing
  easier — the tests here run against real SQLite, where a mocked repository
  would prove nothing.
- **No MediatR.** Three endpoints calling one service directly is clearer than
  three handlers found by reflection.
- **No separate Domain/Application/Infrastructure assemblies.** Project
  boundaries enforce dependency direction, but nothing here is at risk of
  pointing the wrong way, and the cost is four build outputs and a lot of
  `.csproj`.
- **No AutoMapper.** `GoalResponse.From(goal, today)` is eight lines and
  survives a rename.

What *is* enforced:

- **Entities never leave the process.** DTOs are the contract.
- **Endpoints bind, validate, delegate, map.** Nothing that looks like a
  decision lives there.
- **Invariants live in the domain type**, not in the service. `Goal.Create`
  validates everything and throws `DomainValidationException`.
- **The domain reads no ambient state.** `Goal.Create` takes the id and the
  timestamp as arguments; `IIdGenerator` and `TimeProvider` supply them at the
  edge. This is what makes seeds and tests reproducible rather than
  approximately reproducible.
- **Validation exists twice, on purpose.** `CreateGoalRequestValidator` turns a
  request into good field-level messages and reads its limits from the domain
  constants; the domain enforces the same rules regardless of caller.

## Consequences

- A developer looking for "how are goals created?" finds it in one folder.
- `GoalService` talks to `Q2DbContext` directly, so EF features are available
  without fighting an abstraction.
- Swapping the persistence technology would mean touching `GoalService` — an
  accepted trade, and not a scenario anyone has asked for.
- The structure scales by adding folders. If a feature ever grows large enough
  to deserve its own project, extracting one is straightforward because the
  boundary is already a folder.

## Alternatives considered

- **Clean Architecture with four projects.** Real value on a large team with
  several bounded contexts. Here it would be ceremony around one entity.
- **Vertical slice with MediatR.** The organisation is worth copying — and is
  copied — but the mediator itself adds indirection for no benefit at this size.
- **Controllers instead of minimal APIs.** Would work; minimal APIs keep the
  route, the parameters and the result types visible in one place.
