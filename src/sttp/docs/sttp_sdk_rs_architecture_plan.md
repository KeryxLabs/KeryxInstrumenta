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
7. Single AI provider surface for all model interactions

## 3.1 AI Provider Unification (genai + STTP Native)

To keep the SDK ergonomic and composable, all AI interactions should flow through one provider surface.

Candidate dependency:
- genai crate (crates.io package: genai)

Target provider model:
- External providers via genai adapters (OpenAI, Anthropic, Gemini, Ollama, and others supported by genai)
- STTP-native providers as first-class adapters in the same registry

Capabilities to expose through one interface:
- semantic_embedding: RAG/query and memory embedding vectors
- avec_embedding: AVEC-specific embedding or scoring vectors
- avec_scoring: text to AvecState projection when requested

Design outcome:
- MCP and Gateway no longer own provider orchestration logic
- SDK becomes the single place for provider configuration, routing, fallback, and policy

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
- ports: repository, ai provider, clock, id provider

- src/application
- use cases: write/read/find/recall/aggregate/transform/explain
- orchestration and policy enforcement

- src/infrastructure
- adapters for sttp-core-rs NodeStore
- ai adapters (genai external providers + sttp-native providers)
- serialization mappers

- src/interface
- optional request/response DTOs shared by MCP/Gateway adapters

- src/prelude.rs
- ergonomic re-exports for consumers

Suggested domain ports:

- AiProviderRegistry
- resolve(provider_id) -> AiProvider
- list_capabilities()

- AiProvider
- provider_id()
- capabilities()
- embed_semantic(text, options)
- embed_avec(text, options)
- score_avec(text, options)

Notes:
- Not all providers need all capabilities.
- Capability-based dispatch prevents API sprawl and keeps composition explicit.

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

Status update:
- Completed: Phase 0, Phase 1, Phase 2, Phase 3, Phase 4 (SDK-only scope)
- Completed: Initial composition workflows in SDK (recall+explain, daily rollup, capability bundle)
- Completed: transform_then_recall_verify workflow and composition DTO surface
- Completed: manual deterministic compressor with trait-based lexicon provider and request-level lexicon overrides
- In progress: recursive node-from-text composite (spec-aligned content nesting up to depth 5 with deterministic AVEC resolution chain)
- In progress: Phase 5 transport migration (Gateway HTTP/gRPC get_context + list_nodes, graph inventory, and embedding migration preview/run now routed through SDK primitives or SDK-aligned wrappers; MCP get_context/list_nodes routed through SDK primitives)
- Pending: Phase 5 transport migration into MCP/Gateway wrappers

Future deterministic compression and load-testing track:
- [src/sttp/docs/sttp_faker_and_non_ai_compressor_v1.md](src/sttp/docs/sttp_faker_and_non_ai_compressor_v1.md)

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

Phase 6: Recursive deterministic node-from-text composite
- Add composition contract that accepts ordered role-tagged text entries and optional recursive context trees.
- Enforce content nesting <= 5 to remain validator-compatible.
- Deterministic AVEC resolution chain per entry:
	- entry override
	- role override
	- global override
	- optional llm fallback
	- typed failure when unresolved and llm fallback disabled
- Deterministic content construction uses manual compressor outputs (anchor_topic + key_points) for each entry/context node.
- Preserve STTP spine format (no grammar redesign): this phase builds content-layer payloads only.

Phase 6a (current implementation slice)
- Add SDK composition contracts and builder service method for recursive content assembly.
- Add test coverage for:
	- AVEC role/global fallback ordering
	- unresolved AVEC hard-fail policy
	- recursion depth enforcement

Phase 6b (next)
- Add DTO mappings for the new composite contracts.
- Add end-to-end parser/validator conformance tests against generated node payload.
- Add recipe that emits full store-ready node input contract.

## 8. Embedding Logic Relocation Plan

Current embedding logic lives in MCP/Gateway + core service layers.
Target location for policy logic: sttp-sdk-rs/application.

Approach:

1. Keep storage-focused provider contracts in core domain contracts.
2. Introduce SDK AI provider ports and a capability-based provider registry.
3. Implement a genai adapter layer for external providers.
4. Implement STTP-native adapters (local candle and custom AVEC scorers) using the same SDK port.
5. Move embedding input construction policy to SDK (summary + session anchor strategy).
6. Expose embed policy in memory_write and memory_transform.
7. Keep backend storage representation unchanged in v1.

Implementation split:

- core remains storage and domain-contract oriented
- sdk owns provider selection, model routing, fallback policy, and capability checks
- transports call sdk only

Compatibility bridge:

- existing EmbeddingProvider implementations in MCP/Gateway are gradually moved into sdk infrastructure adapters
- existing routes/tools remain stable while internals switch to sdk

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

Sprint 0 (AI Surface Spike):
- Add sttp-sdk-rs dependency spike for genai
- Define AiProvider, AiProviderRegistry, and capability enums
- Build one external adapter (genai + ollama profile) and one sttp-native adapter
- Validate parity for semantic embedding and AVEC scoring paths

Sprint A (Foundational):
- Scaffold sttp-sdk-rs crate
- Define canonical request/response structs:
- MemoryScope
- MemoryFilter
- MemoryPage
- MemoryScoring
- FallbackPolicy
- Define AiTask and ProviderPolicy request primitives:
- task: semantic_embedding | avec_embedding | avec_scoring
- provider_policy: auto | preferred | required
- Implement memory_find + memory_recall

Sprint B (Composability):
- Implement memory_aggregate (session group)
- Implement memory_transform operation embed_backfill
- Add golden tests for strict fallback and aggregation math

Sprint C (Adoption):
- MCP adapters switched to SDK for get_context/list_nodes/migration
- Gateway adapters switched to SDK for equivalent endpoints
- Compatibility tests ensure parity with current behavior

Sprint D (Deterministic Node Construction):
- Ship recursive text-to-content composite in SDK composition layer.
- Add strict schema conformance tests with sttp-core-rs parser and validator.
- Add optional llm AVEC bridge only when deterministic chain cannot resolve AVEC.
- Add example workflow: array + options input -> spec-safe content layer payload.

## 12. Definition of Done for v1

- sttp-sdk-rs published as crate
- Primitive APIs usable directly from Rust consumers
- MCP + Gateway use SDK internally for retrieval and migration flows
- Strict fallback policy behavior preserved and test-covered
- Session-level aggregate metrics available through memory_aggregate
- AI provider interactions unified behind SDK capability-based interface
- At least one genai-backed provider and one sttp-native provider production wired
- Deterministic recursive text-to-content composite available with depth and AVEC policy controls
