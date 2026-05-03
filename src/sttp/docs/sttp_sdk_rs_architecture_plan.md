# STTP SDK Rust Architecture Plan

Date: 2026-05-03
Status: Draft v1

## 1. Why SDK First

The current shape is transport-first (MCP and Gateway) with useful but endpoint-centric tools.
For long-term standardization, the right center is an SDK-first memory algebra:

- Minimal orthogonal primitives
- Transport-agnostic contracts
- Composable operations
- Stable semantics across MCP, HTTP, gRPC, and local embedding runtimes

This document defines that target and an incremental migration path with low break risk.

## 2. Product Goal and Scope

### Goal

Build sttp-sdk-rs as the canonical memory substrate for AI integrations, where higher-level workflows are compositions of primitives.

### Non-goals for v1

- No breaking removal of existing MCP/Gateway endpoints
- No mandatory storage backend rewrite
- No large grammar/schema redesign in STTP node format

## 3. Design Principles

1. Primitive-first
2. Transport-neutral
3. Typed contracts with explicit policy controls
4. Backward compatibility by adapter layers
5. Explainability for ranking decisions
6. Deterministic behavior under strict retrieval modes

## 4. Primitive Set (SDK v1)

## 4.1 memory_write

Purpose: Idempotent write/upsert for one or many nodes.

Inputs:
- tenant_id optional
- items[]
- write_policy: upsert_by_sync_key | force_new
- embed_policy: none | if_missing | always

Outputs:
- per-item status: created | updated | duplicate | skipped | failed
- node_id, sync_key, embedded(bool), error(optional)
- summary counters

## 4.2 memory_read

Purpose: Deterministic retrieval by identity.

Inputs:
- node_ids[] optional
- sync_keys[] optional
- projection fields optional

Outputs:
- items[]
- not_found[]

## 4.3 memory_find

Purpose: Non-ranked filtered selection.

Inputs:
- scope: tenant_id, session_ids, tiers, from_utc, to_utc
- filters: has_embedding, embedding_model, metric ranges, text contains
- sort: field + direction
- page: limit + cursor

Outputs:
- items[]
- page: next_cursor, has_more
- stats: matched_count

## 4.4 memory_recall

Purpose: Ranked retrieval.

Inputs:
- scope/filter/page (same model as memory_find)
- query: current_avec, query_text, query_embedding
- scoring: channel weights for resonance, semantic, lexical
- policy: fallback_policy = never | on_empty | always
- strictness: precision | balanced | recall

Outputs:
- items[] with channel scores + final score
- retrieval_path: resonance_only | semantic_only | hybrid | lexical_fallback
- stats + page

## 4.5 memory_aggregate

Purpose: Group and reduce.

Inputs:
- scope/filter
- group_by: session_id | tier | embedding_model | date_bucket
- metrics: count, embedding coverage, avg/blended AVEC, psi/rho/kappa stats

Outputs:
- groups[]
- totals

## 4.6 memory_transform

Purpose: Bulk mutation by selector.

Inputs:
- selector = scope/filter
- operation: embed_backfill | reindex_embeddings | patch_metadata | retier
- dry_run
- execution controls: batch_size, max_nodes, checkpoint

Outputs:
- execution summary
- per-batch and per-session counters
- failures[]

## 4.7 memory_explain

Purpose: Explain recall outcome.

Inputs:
- request payload or trace_id

Outputs:
- stage counts
- dropped-by-filter reasons
- ranking contribution per channel
- fallback trigger reason

## 4.8 memory_schema

Purpose: Introspection for dynamic clients.

Outputs:
- supported fields/operators/group_by/metrics
- scoring channels and defaults
- policy enums and limits

## 5. SDK Package Architecture (DDD + Hexagonal)

Proposed new crate: src/sttp/sttp-sdk-rs

Layout:

- src/domain
- entities: memory node projections and derived metrics
- value objects: scope, filter, policy, score vectors
- ports: repository, embedder, clock, id provider

- src/application
- use cases: write/read/find/recall/aggregate/transform/explain
- orchestration and policy enforcement

- src/infrastructure
- adapters for sttp-core-rs NodeStore
- embedding adapters (ollama, candle-local)
- serialization mappers

- src/interface
- optional request/response DTOs shared by MCP/Gateway adapters

- src/prelude.rs
- ergonomic re-exports for consumers

## 6. Compare to Existing Surface

Existing MCP tools in main server:
- calibrate_session
- store_context
- get_context
- list_nodes
- preview_embedding_migration
- run_embedding_migration
- get_moods
- create_monthly_rollup

Mapping:

- store_context -> memory_write adapter
- list_nodes -> memory_find adapter
- get_context -> memory_recall adapter
- preview/run_embedding_migration -> memory_transform adapter
- create_monthly_rollup -> memory_aggregate specialized recipe
- calibrate_session/get_moods remain domain-specialized, not core primitives

Existing Gateway HTTP models already overlap with future primitive contract vocabulary and can be adapted incrementally.

## 7. Integration Strategy

Phase 0: Scaffold
- Create sttp-sdk-rs crate with module boundaries and core request/response contracts.
- Add dependency on sttp-core-rs.

Phase 1: Primitive adapters over current core
- Implement memory_find and memory_recall first using existing ContextQueryService and NodeStore calls.
- Preserve strict fallback behavior in memory_recall (on_empty default).

Phase 2: Aggregate primitive
- Implement memory_aggregate grouped by session_id with:
- node_count
- embedding_coverage
- avg user/model/compression AVEC
- blended AVEC options
- psi range

Phase 3: Transform primitive
- Wrap existing EmbeddingMigrationService logic as memory_transform operation embed_backfill/reindex_embeddings.

Phase 4: Explain and schema
- Add memory_explain and memory_schema for observability and client auto-discovery.

Phase 5: Transport migration
- MCP and Gateway call SDK primitives internally.
- Keep old endpoint names as compatibility wrappers for at least one release cycle.

## 8. Embedding Logic Relocation Plan

Current embedding logic lives in MCP/Gateway + core service layers.
Target location for policy logic: sttp-sdk-rs/application.

Approach:

1. Keep low-level provider interfaces in core domain contracts.
2. Move embedding input construction policy to SDK (summary + session anchor strategy).
3. Expose embed policy in memory_write and memory_transform.
4. Keep backend storage representation unchanged in v1.

## 9. Compatibility and Versioning

- SDK SemVer: start at 0.x while contract stabilizes.
- MCP/Gateway keep current endpoints; mark as compatibility adapters.
- Add deprecation notes after primitive tools are production proven.

## 10. Risks and Mitigations

Risk: Contract sprawl before adoption
- Mitigation: ship only four primitives first (find, recall, aggregate, transform)

Risk: Behavior drift across transports
- Mitigation: all transports call SDK use cases, not duplicated logic

Risk: Overfitting to migration diagnostics
- Mitigation: operation model in memory_transform is generic and composable

## 11. Immediate Sprint Plan

Sprint A (Foundational):
- Scaffold sttp-sdk-rs crate
- Define canonical request/response structs:
- MemoryScope
- MemoryFilter
- MemoryPage
- MemoryScoring
- FallbackPolicy
- Implement memory_find + memory_recall

Sprint B (Composability):
- Implement memory_aggregate (session group)
- Implement memory_transform operation embed_backfill
- Add golden tests for strict fallback and aggregation math

Sprint C (Adoption):
- MCP adapters switched to SDK for get_context/list_nodes/migration
- Gateway adapters switched to SDK for equivalent endpoints
- Compatibility tests ensure parity with current behavior

## 12. Definition of Done for v1

- sttp-sdk-rs published as crate
- Primitive APIs usable directly from Rust consumers
- MCP + Gateway use SDK internally for retrieval and migration flows
- Strict fallback policy behavior preserved and test-covered
- Session-level aggregate metrics available through memory_aggregate
