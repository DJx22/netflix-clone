# ADR 0008: Defer Podman Parity

**Status:** Accepted  
**Date:** 2026-10-07  
**Deciders:** Dhruv (project owner)

## Context

Phase 3 planned to run the Docker Compose platform under Podman and document the
differences. During setup and attempted validation on the current Windows
environment, Podman introduced significant performance issues. Continuing to
troubleshoot or optimize Podman would delay the next planned learning phase.

## Decision

Defer Phase 3 and proceed directly to Phase 4 (Gateway + RabbitMQ). Keep Docker
Compose as the local container workflow for the remaining planned phases. Do not
spend further effort on Podman parity during this pass.

After the planned phases are complete, reconsider Podman parity and add it to the
backlog if it is still valuable.

## Consequences

- The project does not currently claim Podman parity.
- Phase 4 can proceed without additional Podman setup or performance work.
- Podman-specific behavior and Windows differences remain undocumented until the
  phase is revisited.

## Revisit trigger

Revisit after the planned phases are complete, or sooner if Podman performance
improves enough in the development environment to make parity work worthwhile.
