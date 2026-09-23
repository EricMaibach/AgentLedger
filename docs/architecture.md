# Architecture and Engineering Approach

How AgentLedger is built, and why. It covers the principles, the layer rules, the patterns we use and the testing standards. It's meant to change rarely. Specific decisions and their reasoning are recorded in [`adr/`](adr/); what to build next is in [`roadmap.md`](roadmap.md).

If you are an AI agent working on this repository: follow this document. If a task seems to need something that contradicts it, stop and raise it instead of working around it.

## Principles

- **Clean Architecture.** Dependencies point inward. The domain knows nothing about databases, HTTP or frameworks.
- **Domain-Driven Design.** The domain model is expressed in code with rich types: aggregates, entities, value objects. Primitives stand in for domain concepts only when no better type exists.
- **SOLID.** Especially:
  - **single responsibility:** one reason to change per class;
  - **open/closed:** adding an agent adds an adapter, not Core changes;
  - **dependency inversion:** Core and UseCases define interfaces, and Infrastructure implements them.
- **Composition over inheritance.** Build behavior by combining small objects behind interfaces, injected through DI, rather than by extending classes.
  - **Don't create our own base classes** to share code or behavior. Extract a collaborator (a service, value object or policy) and inject it.
  - **Classes and records are `sealed`** unless deliberately designed for inheritance, so the compiler enforces this rule. Constants-only classes are `static`.
  - **Library base types are the exception.** Inheriting from a library's designated base type (`EntityBase`, `SmartEnum`, FastEndpoints' `Endpoint`, EF's `DbContext`) is allowed: one level deep, and only because that is how the library is meant to be extended.
  - **Variation goes through strategies, not subclasses.** Example: each agent's adapter implements a common interface (`IAgentAdapter`) and receives shared logic (payload reading, tool-call pairing) as injected helpers. There is no adapter base class. To change one agent's behavior, inject a different helper, never override.
  - **Cross-cutting concerns** go in Mediator pipeline behaviors or decorators, not base-class hooks.
- **Make invalid states unrepresentable.** Validate at construction. An object that exists is valid.
- **Start simple and evolve.** Build for today's requirement. No speculative code, abstractions or "just in case" features. A pattern in the [catalogue](#pattern-catalogue) marked *available* is pulled in the first time it's needed, not before. When it is needed, use it: don't improvise an alternative.
- **Keep the raw truth.** Anything derived can be rebuilt from what was captured (see [ADR 0001](adr/0001-raw-event-ledger-with-projections.md)).

## Solution structure

The structure follows the multi-project layout of the `Ardalis.CleanArchitecture.Template`.

```
src/
  AgentLedger.Core             domain model: aggregates, entities, value objects, domain events, specifications, interfaces
  AgentLedger.UseCases         application logic: commands, queries, handlers, DTOs
  AgentLedger.Infrastructure   EF Core + Npgsql, repositories, external services; implements Core/UseCases interfaces
  AgentLedger.ServiceDefaults  OpenTelemetry, health checks, resilience defaults
  AgentLedger.Web              HTTP API (FastEndpoints), composition root, configuration
tests/
  AgentLedger.UnitTests        Core and UseCases in isolation
  AgentLedger.IntegrationTests Infrastructure against real Postgres (Testcontainers)
  AgentLedger.FunctionalTests  the whole API over HTTP against real Postgres (Testcontainers)
```

### Dependency rules

```
Web ──► UseCases ──► Core
 │                    ▲
 └──► Infrastructure ─┘  (also references UseCases for query-service interfaces)
```

- **Core** references no other project in the solution, and no infrastructure packages (no EF Core, ASP.NET or Npgsql). The only packages it uses are for domain building blocks: `Ardalis.GuardClauses`, `Ardalis.Result`, `Ardalis.SharedKernel`, `Ardalis.SmartEnum`, `Ardalis.Specification`, `Mediator.Abstractions` and `Vogen`.
- **UseCases** references Core only. Handlers depend on interfaces (`IRepository<T>`, `IReadRepository<T>`, query-service interfaces), never on `AppDbContext`.
- **Infrastructure** implements interfaces declared in Core and UseCases. This is where database, file system and network code lives.
- **Web** wires everything together (the composition root) and turns HTTP into commands and queries. Endpoints hold no business logic.
- **Separate clients** (e.g. the future `agentledger` CLI) do not reference Core. They talk to the API over HTTP.

## Pattern catalogue

*In use* means it's in the code today. *Available* means it's the approved way to solve that problem when it first comes up.

### Domain (Core)

| Pattern | Status | When and how |
|---|---|---|
| **Aggregate root** | In use | Derive from `EntityBase<TSelf, TId>` and implement `IAggregateRoot`. Only aggregate roots get repositories. Group each aggregate in a folder: `Core/<Name>Aggregate/`. |
| **Strongly-typed ID** | In use | A Vogen `[ValueObject<Guid>] readonly partial struct` with a `Validate` method. Never use bare `Guid`/`int` for identity. The `VogenDefaults` assembly attribute lives in `AgentEventId.cs`; there is one per assembly. |
| **Value object (single value)** | In use | Vogen, as for IDs. Use it for a primitive with rules (e.g. an email address, a bounded string). |
| **Value object (multiple values)** | In use | A positional `record`, for value equality and immutability. Example: `CaptureContext`. |
| **Fixed set of values** | In use | A `sealed` `SmartEnum<T>` with a `private` constructor, rather than a C# `enum`: it rejects invalid values and can carry behavior. Use `nameof` for names. **Never renumber values once persisted.** |
| **Guard clauses** | In use | Validate every constructor input with `Guard.Against.*`, and assign the returned value (`X = Guard.Against.NullOrWhiteSpace(x)`). Use `Guard.Against.Default` for structs such as `DateTimeOffset`, because `Null` never fires on a struct. |
| **Immutability** | In use | Properties are `{ get; private set; }`, and there are no mutators unless the domain needs them. Collections passed in are **copied** in the constructor (defensive copy) and exposed read-only. |
| **No ambient time or randomness** | In use | Entities never call `DateTime.UtcNow`, `DateTimeOffset.Now` or `Guid.NewGuid()`. Times and IDs are passed in; handlers get time from `TimeProvider` (injected). |
| **Domain events** | Available | Raise them with `RegisterDomainEvent(...)` from the aggregate. They are dispatched after a successful save by `EventDispatchInterceptor`, and handled in Core or UseCases via Mediator notification handlers. Use them when one aggregate's change must trigger another action (e.g. updating a projection). |
| **Specification** | Available | `Ardalis.Specification`, for any query that filters beyond lookup by ID. Specifications live beside their aggregate (`<Name>Aggregate/Specifications/`). Don't write LINQ against `DbContext` in handlers. |
| **Domain service** | Available | For logic that spans aggregates and doesn't belong to one. It's an interface plus implementation in `Core/Services/`. |

### Application (UseCases)

| Pattern | Status | When and how |
|---|---|---|
| **Command / query + handler** | Available (next up) | Source-generated `Mediator`. One folder per use case: `UseCases/<Feature>/<Action>/` holding `<Action>Command` (or `Query`) and `<Action>Handler`. Commands change state; queries don't. |
| **`Result<T>`** | Available (next up) | Handlers return `Ardalis.Result` (`Success`, `Invalid`, `NotFound`, `Conflict`, `Error`) for expected outcomes. Exceptions are only for bugs and infrastructure failures. |
| **Repository** | Available | `IRepository<T>` for writes and `IReadRepository<T>` for reads, both implemented once by the generic `EfRepository<T>`. Don't write one repository per entity. |
| **Query service** | Available | For read models that bypass the domain (reporting, lists, projections): an interface in UseCases, implemented in Infrastructure with EF or SQL. |
| **Paging** | Available | `PagedResult<T>`, with limits from `UseCases/Constants.cs`. |
| **Pipeline behaviors** | In use | Cross-cutting concerns (logging, validation) go in Mediator pipeline behaviors, registered in `MediatorConfig.cs`, not in handlers. |

### Infrastructure

| Pattern | Status | When and how |
|---|---|---|
| **EF Core configuration** | Available (next up) | One `IEntityTypeConfiguration<T>` per aggregate in `Infrastructure/Data/Config/`, picked up automatically. Map Vogen types with value converters. Postgres-specific types (`jsonb`, GIN indexes) are chosen here, never in Core. |
| **Migrations** | Available | EF Core migrations in `Infrastructure/Data/Migrations/`. Applied automatically in Development and on demand elsewhere (`Database:ApplyMigrationsOnStartup`). |
| **Configuration** | In use | The connection string is `ConnectionStrings:AgentLedger`, supplied by the environment. No secrets in `appsettings*.json`. |

### Web

| Pattern | Status | When and how |
|---|---|---|
| **REPR endpoint** | In use | FastEndpoints, one class per endpoint (Request, Endpoint, Response), grouped by feature folder. The endpoint validates the request shape, sends a command or query, and maps the `Result` to HTTP with `Extensions/ResultExtensions.cs`. |
| **Request validation** | Available | FastEndpoints validators (FluentValidation) check the request *shape*. Domain rules stay in the domain. |
| **API docs** | In use | An OpenAPI document is generated by FastEndpoints; the Scalar UI serves it in Development. |

## Testing

- **Test first.** Write the tests before the implementation, and **see each new test fail for the right reason** before making it pass. A test that is green before its implementation exists is broken.
- **Unit tests:** Core and UseCases, with no I/O. Use NSubstitute for interfaces, and `NoOpMediator` where a handler needs a mediator.
- **Integration tests:** Infrastructure against **real Postgres via Testcontainers** (`postgres:18`, matching the dev database). Never use the EF in-memory provider; it hides SQL and `jsonb` behavior.
- **Functional tests:** the full API through `CustomWebApplicationFactory`, which boots the real app pointed at a Testcontainers database.
- **Test Data Builders:** each aggregate gets a builder (e.g. `AgentEventBuilder`) that produces a valid object. Tests change only the value they are about.
- **Naming:** one test class per behavior of the unit under test (`AgentEventConstructor`, `AgentEventIdFrom`); method names state the expected behavior (`RejectsMissingPayload`).
- **Assertions:** Shouldly, with one exception: for "throws" tests, use `Assert.ThrowsAny<TException>(...)` (accepts subclasses) or `Should.Throw<T>`. **Do not** use `Record.Exception(...)` followed by `ShouldBeAssignableTo<T>()`: it passes when nothing is thrown.
- **Never weaken, skip or delete a test to get to green.** If a test is wrong, fix it and say why.

## Conventions

- **.NET 10, C# latest.** File-scoped namespaces and primary constructors where they aid clarity, target-typed `new`, records for value objects.
- **Nullable reference types on, warnings are errors** (`Directory.Build.props`). Don't silence warnings with `!` or `#pragma` without a comment explaining why.
- **Central package management:** versions only in `Directory.Packages.props`; project files list package names.
- **Formatting:** `.editorconfig` defines it (2-space indent, LF line endings). Run `dotnet format` before finishing work; `dotnet format --verify-no-changes` must pass.
- **Global usings** per project in `GlobalUsings.cs` for the common framework namespaces.
- **Comments explain *why*,** not what the code does.

## Working with AI agents

This project is developed with AI coding agents, and is itself a tool for observing them. The same approach applies to both:

- **Types and tests are the guardrails.** Strong types turn mistakes into compile errors, and tests define "done" objectively. Agents get fast, precise feedback from the compiler and test runner, so prefer rules the compiler or a test can check over rules only a reviewer can.
- **Every abstraction costs context.** Keep files focused and the structure predictable, so an agent can find the right place without reading everything. Add structure where it prevents real mistakes, not by reflex.
- **Discuss design before building** anything that adds a project, a table, a public API or a new pattern. Record significant decisions as an ADR.
- **Leave the build green:** `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` all pass at the end of every change.
