# 0007. Ardalis Clean Architecture template, without the Aspire AppHost

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

We wanted a well-understood Clean Architecture structure rather than a hand-rolled one. The `Ardalis.CleanArchitecture.Template` (v11.1.1) provides:
- the Core/UseCases/Infrastructure/Web split;
- FastEndpoints and source-generated Mediator;
- the Ardalis Result/Specification/GuardClauses/SmartEnum libraries;
- Vogen;
- unit, integration and functional test projects.

It also ships:
- a sample domain, email support, and SQL Server/SQLite;
- a .NET Aspire AppHost that orchestrates SQL Server.

The development environment is already a Compose-based dev container that runs Postgres.

## Decision

- Generate the solution from the template, then remove the sample domain, email, SQL Server/SQLite and unused scaffolding.
- **Remove the Aspire AppHost.** Infrastructure is orchestrated by Docker Compose (dev container), which Aspire would duplicate.
- **Keep ServiceDefaults** for OpenTelemetry, health checks and resilience, so the API's own behavior stays observable.
- Upgrade template package pins to current patch versions (several had known vulnerabilities).

## Consequences

- The structure and patterns match a widely used reference, which is easy for contributors and agents to recognize.
- There's no Aspire dashboard. Local telemetry needs an OTLP endpoint if we want to view it.
- Some template conventions are kept deliberately, even where they are more than a small service needs today (see the pattern catalogue in `docs/architecture.md`).
