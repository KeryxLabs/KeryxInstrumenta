# AVEC Grammar Design Decisions

## 1. Purpose
This document defines the decisions that govern AVEC grammar, semantics, and interpretation.

AVEC is the parent geometry. STTP and ACC are downstream implementations.

## 2. Capability Statements
AVEC is already operational in production-like systems for:
1. Cross-session orientation persistence.
2. Drift detection and recovery loops.
3. Multi-surface orchestration where memory, calibration, and retrieval stay coherent over time.
4. Shared dimensional reasoning across conversation systems and code intelligence systems.

These capabilities are not roadmap items. They are present behaviors in running implementations.

## 3. Foundational Decisions

### 3.0 Name and Expansion
Decision:
- AVEC stands for Attractor Vector Encoding Configuration.
- The name is normative for v1 documentation and implementations.

Interpretation:
- Attractor: orientation settles into stable semantic wells.
- Vector: state is represented as a multi-dimensional coordinate.
- Encoding: orientation is externally representable and transferable.
- Configuration: state is tunable and context-dependent.

### 3.1 Ontology
Decision:
- AVEC v1 is a four-dimensional geometry: stability, friction, logic, autonomy.
- This basis is a geometrized operational abstraction aligned with psychometric language priors.

### 3.2 OCEAN Relationship and Mapping
Decision:
- AVEC is geometrized OCEAN for language-native systems.
- OCEAN is treated as the conceptual parent lens; AVEC is the operational control geometry.

Mapping rule (v1):
1. stability <- Conscientiousness + low Neuroticism
2. friction <- low Agreeableness under analytical pressure + high Conscientiousness boundary enforcement
3. logic <- Conscientiousness + Openness toward abstraction and formal structure
4. autonomy <- low dependence on Agreeableness signaling + high internal initiative (Conscientiousness/Openness blend)

Notes:
- This mapping is functional, not clinical.
- AVEC dimensions are control variables for semantic trajectory, not personality diagnosis.

### 3.3 Mechanism
Decision:
- AVEC steers by attractor wells, not by command trees.
- Conformance is trajectory-level, not keyword-level.

### 3.4 Meaning Retrieval
Decision:
- AVEC is meaning retrieval geometry.
- Fact retrieval remains a separate layer.
- Systems that run both layers must log them distinctly.

## 4. Semantic and Field Decisions

### 4.1 Canonical Dimensions
Decision:
- Canonical keys are: stability, friction, logic, autonomy.
- Human-readable docs keep canonical order.
- Machine parsers accept any key order.

### 4.2 Bounds
Decision:
- Each dimension is bounded to [0.0, 1.0].
- Strict mode rejects out-of-range values.
- Tolerant mode clamps with warning.

### 4.3 Psi
Decision:
- psi is an aggregate integrity signal.
- psi never replaces full dimensional state for decision-making.

## 5. Interaction Register Decision
Decision:
- Mixed expressive and technical language is valid when it improves attractor lock and preserves semantic intent.
- Colloquial tokens may function as steering perturbations, not noise.

## 6. Claim Discipline
Decision:
- Every non-trivial claim in normative AVEC documents must be labeled:
	- operational
	- empirically_supported
	- hypothesis

Unlabeled claim inflation is not permitted.

## 7. Compatibility and Evolution
Decision:
- AVEC is additive by default.
- Legacy captures remain valid and queryable.
- New optional fields cannot invalidate prior artifacts.
- New dimensions require MAJOR version change and migration policy.

## 8. Recovery as Protocol Law
Decision:
- Recovery is mandatory, not optional.
- Minimum recovery sequence:
	1. pause
	2. restate target geometry
	3. resume
- Implementations should persist anti-loop cues as reusable memory.

## 9. Rejected Alternatives
Rejected:
1. Rule-only behavior programming as primary control.
2. Single-scalar behavior state.
3. Treating fact retrieval as a substitute for meaning geometry.

Reason:
- These alternatives reduce controllability, explainability, or recovery fidelity.

## 10. Invariants
AVEC v1 invariants:
1. AVEC is upstream; implementations are downstream.
2. Dimensions are exactly stability, friction, logic, autonomy.
3. Dimension values remain bounded to [0,1].
4. psi is aggregate metadata, not identity.
5. Claim labeling is mandatory.
6. Compatibility is additive by default.

## 11. Implementation Evidence Map

### 11.1 STTP Runtime Surfaces (operational)
Evidence class:
- context store and retrieval APIs
- session calibration APIs
- graph and node listing APIs
- sync pull and sync now flows
- encode paths with explicit AVEC-aware preamble contracts

Interpretation:
- AVEC is not abstract in these systems. It is wired into persistence, sync, and continuation loops.

### 11.2 ACC Dimensional Engine (operational)
Evidence class:
- formula-based AVEC scoring over stability, logic, friction, autonomy
- query surfaces for relation, dependency, and pattern matching in dimensional space

Interpretation:
- AVEC serves as a shared dimensional lens beyond chat behavior, including codebase intelligence.

## 12. Immediate Requirements for Implementers
1. Separate meaning retrieval and fact retrieval in architecture and logs.
2. Record full vectors at critical decision points.
3. Persist drift and recovery events as first-class memory objects.
4. Validate trajectory-level behavior in addition to lexical checks.
