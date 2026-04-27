# STTP Grammar Blueprint
## State-Flow Implementation Plan for a Typed IR of Cognition

Status: Draft
Owner: STTP Core
Last Updated: 2026-04-25

## 1. Why This Exists
STTP is not a serialization convenience. It is a typed IR for cognition state transfer.

The parser therefore has two obligations at once:
1. Accept and reconstruct meaning from real-world node traffic, including legacy variants.
2. Enforce a canonical contract strict enough to support machine verification, composability, and long-term language evolution.

This blueprint is the engineering choreography for that dual obligation.

Normative language rules live in:
- sttp_typed_ir_language_spec.md

## 2. First Principles
1. Grammar is not cosmetic. Grammar is the boundary between meaning and noise.
2. Layer order is semantic order, not display order.
3. Canonical AST is the integration surface. Raw text is evidence, not structure.
4. Backward compatibility is achieved by tolerant ingestion, not by lowering strict standards.
5. Sync and dedupe identity are out of scope and MUST remain unchanged.

## 3. Core Objectives
1. Deterministic state-machine parse path with explicit transitions.
2. Rich diagnostics that explain failure and recovery, not just pass/fail.
3. Strict profile for conformance and generation.
4. Tolerant profile for archival and operational resilience.
5. Stable canonical AST contract across formatting styles.

## 4. Guardrails and Boundaries
### 4.1 Must Not Change in This Work
1. sync_key behavior.
2. dedupe semantics.
3. storage identity behavior.
4. historical raw payload bytes.

### 4.2 Additive Compatibility Policy
1. Older nodes remain queryable.
2. New strict nodes remain strongly validated.
3. Tolerant mode bridges old forms into canonical AST with diagnostics.

## 5. Inputs and Authority
Normative source:
1. store_context schema contract in sttp-mcp.

Operational source:
1. historical node corpus in sttp-mcp data snapshots.

Decision rule:
1. Strict profile follows normative source.
2. Tolerant profile follows explicitly documented compatibility matrix.

## 6. State-Flow Architecture
### 6.1 Top-Level States
1. Start
2. InProvenance
3. InEnvelope
4. InContent
5. InMetrics
6. Done
7. Error

### 6.2 Strict Transition Spine
1. Start -> InProvenance
2. InProvenance -> InEnvelope
3. InEnvelope -> InContent
4. InContent -> InMetrics
5. InMetrics -> Done

This spine is non-negotiable for strict validity.

### 6.3 Tolerant Recovery Edges
1. Missing expected layer marker may advance with error diagnostics if subsequent layer boundaries are unambiguous.
2. Field-level parse failure may keep container state alive when structural boundaries remain intact.
3. Legacy compact separators may be tokenized and normalized into canonical key-value forms.

Recovery is conditional, never silent.

## 7. Lexer Responsibilities
The lexer is responsible for structural truth before semantic interpretation.

Token classes:
1. protocol markers and stops.
2. object and array punctuation.
3. separators, including compatibility separators.
4. identifiers and confidence signatures.
5. scalar literals.

Lexer principles:
1. No regex ambiguity loops in hot path.
2. Position tracking for every token.
3. Preserve enough token evidence for deterministic diagnostics.

## 8. Canonical AST Contract
Each parse result MUST expose:
1. raw source text.
2. canonical provenance/envelope/content/metrics objects.
3. parse profile used.
4. diagnostics list.
5. strict_valid flag.
6. recoverability metadata.

Canonical AST goals:
1. stable shape for services and storage adapters.
2. deterministic ordering for comparisons.
3. extension buckets for unknown fields.

## 9. Diagnostics Model
Severity levels:
1. Fatal: no structurally recoverable node.
2. Error: recoverable parse but strict-invalid.
3. Warning: normalized compatibility deviation.
4. Info: benign non-canonical formatting.

Diagnostic record SHOULD include:
1. machine code.
2. short message.
3. source span.
4. recovery action.
5. strict impact.

## 10. Compatibility Matrix
### 10.1 Accepted Legacy Forms
1. optional outer node marker.
2. absent internal node wrappers.
3. pipe-delimited compact payloads.
4. mixed quoted and unquoted enum-like values.
5. parent reference represented as ref token, quoted token, or plain token.

### 10.2 Rejected Forms
1. unrecoverable layer boundary ambiguity.
2. unbalanced container nesting with no deterministic repair path.
3. non-tokenizable confidence fields.
4. required numeric fields with irrecoverable token corruption.

## 11. Normalization Contract
Normalization is a projection into canonical AST, not a rewrite of history.

Required normalization:
1. map known aliases to canonical keys.
2. normalize parent references.
3. coerce unambiguous numeric strings.
4. split compact separators into canonical pairs.
5. preserve unknown fields in extension maps.

Forbidden normalization:
1. mutate raw payload.
2. fabricate missing semantic values.
3. erase unknown fields without trace.
4. alter identity-sensitive behavior.

## 12. Adversarial Robustness Requirements
Threat classes parser must explicitly handle:
1. layer confusion and marker spoofing.
2. confidence laundering through malformed field signatures.
3. delimiter smuggling in compact forms.
4. partial object poisoning where one layer attempts to bleed into another.

Required response:
1. strict mode fails closed.
2. tolerant mode fails informative with bounded recovery.
3. both modes emit deterministic diagnostics.

## 13. Test Program
Test suites:
1. canonical strict fixtures.
2. real historical tolerant fixtures.
3. parser differential tests against existing behavior.
4. fuzzed formatting permutations.
5. adversarial fixtures by threat class.

Pass gates:
1. strict fixtures produce zero errors.
2. tolerant fixtures recover as expected with stable diagnostics.
3. no sync or dedupe regressions.

## 14. Rollout State Flow
State A:
1. parser runs in shadow mode alongside regex parser.
2. compare AST and diagnostics in tests.

State B:
1. tolerant read path defaults to state machine parser.
2. strict path remains gated while conformance matures.

State C:
1. state machine parser is primary parser.
2. regex parser retired after stability thresholds are met.

## 15. Definition of Done
1. strict and tolerant profiles implemented.
2. canonical AST contract stabilized.
3. compatibility matrix codified in tests.
4. adversarial fixture suite passing.
5. zero regressions in retrieval/storage flows.
6. sync and dedupe behavior unchanged.

## 16. Open Questions
1. canonical encoding policy for reference tokens containing Unicode node symbols.
2. permanent acceptance policy for bare ISO timestamps in tolerant mode.
3. default severity for unknown top-level keys.

## 17. Blueprint Promise
When complete, STTP parsing behaves as a language runtime:
1. strict enough to be trusted,
2. tolerant enough to preserve continuity,
3. explicit enough to evolve without drift.
