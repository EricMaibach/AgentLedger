# 0008. Read-only MCP server for AI access

- **Status:** Accepted
- **Date:** 2026-09-23

## Context

One of the main uses of the ledger is having AI review what other AI agents did. The Model Context Protocol (MCP) is the standard way to give agents tools.

## Decision

- Provide an **MCP server** as another entry point (alongside the HTTP API) over the **query use cases only**.
- It connects with a Postgres role that has **SELECT only**.
- It's built with the official C# `ModelContextProtocol` SDK.
- Tool results return summaries first and drill down on request, to protect the caller's context window.
- Logged content is returned **explicitly marked as data**. Logged tool output can contain prompt-injection text, and must never be presented as instructions.

## Consequences

- No write path through MCP, enforced by the database role as well as by code.
- Query use cases must exist before the MCP server is useful, so it comes after projections.
