# AVEC Grammar Specification (v1.0)

## 1. Scope
This is the normative grammar and validation contract for AVEC.

AVEC is implementation-neutral and upstream of protocol-specific runtimes.

## 2. Capability-Oriented Semantics
AVEC vectors encode orientation geometry used for continuity, calibration, and trajectory control.

The four canonical dimensions:
1. stability
2. friction
3. logic
4. autonomy

These dimensions are interpreted as operational controls, not stylistic tags.

## 3. Core Types

### 3.1 Scalar
Scalar := float in [0.0, 1.0]

### 3.2 AvecVector
Required keys:
- stability: Scalar
- friction: Scalar
- logic: Scalar
- autonomy: Scalar

Optional key:
- psi: float

Dimension semantics:
- stability: persistence of trajectory under perturbation
- friction: critical resistance applied during reasoning
- logic: formal rigor pressure
- autonomy: stance and initiative independence

### 3.3 ClaimLabel
ClaimLabel := operational | empirically_supported | hypothesis

### 3.4 RetrievalLayer
RetrievalLayer := knowledge | meaning | hybrid

Layer semantics:
- knowledge: factual retrieval plane
- meaning: orientation geometry retrieval plane
- hybrid: both planes active

## 4. Canonical Shapes

### 4.1 Bare AVEC
{
  "stability": 0.82,
  "friction": 0.66,
  "logic": 0.91,
  "autonomy": 0.95,
  "psi": 3.34
}

### 4.2 Single-Vector Envelope
{
  "schema_version": "avec-1.0",
  "vector": {
    "stability": 0.82,
    "friction": 0.66,
    "logic": 0.91,
    "autonomy": 0.95,
    "psi": 3.34
  },
  "claim_label": "empirically_supported",
  "retrieval_layer": "meaning",
  "notes": "optional"
}

### 4.3 Dual-Vector Interaction Envelope
{
  "schema_version": "avec-1.0",
  "user_avec": {
    "stability": 0.82,
    "friction": 0.66,
    "logic": 0.91,
    "autonomy": 0.95,
    "psi": 3.34
  },
  "model_avec": {
    "stability": 0.935,
    "friction": 0.163,
    "logic": 0.9458,
    "autonomy": 0.84375,
    "psi": 2.88755
  },
  "claim_label": "operational",
  "retrieval_layer": "hybrid"
}

## 5. Human-Readable Grammar (ABNF)
avec-vector = "{" ws dim ws "," ws dim ws "," ws dim ws "," ws dim [ ws "," ws psi ] ws "}"
dim         = ("stability" / "friction" / "logic" / "autonomy") ws ":" ws scalar
psi         = "psi" ws ":" ws number
scalar      = number ; MUST satisfy 0.0 <= value <= 1.0
number      = ["-"] 1*DIGIT ["." 1*DIGIT]
ws          = *WSP

Note:
- Canonical key order is required in docs and renderers.
- Parsers must accept arbitrary key order in machine formats.

## 6. Validation Contract

### 6.1 Strict Mode
1. All required vector keys present.
2. No unknown keys unless namespaced (example: x_vendor_key).
3. All dimension values in [0,1].
4. claim_label present for normative artifacts.
5. retrieval_layer present when retrieval context is declared.

### 6.2 Tolerant Mode
1. Missing psi accepted.
2. Unknown keys accepted with warning.
3. Out-of-range values clamped to [0,1] with warning.
4. Missing claim_label defaults to hypothesis with warning.

## 7. Derived Metrics
psi_sum := stability + friction + logic + autonomy

Rules:
1. Consumers compute psi_sum.
2. If explicit psi exists and abs(psi - psi_sum) > epsilon, emit drift warning.
3. Default epsilon is 0.02 unless implementation defines another value.

## 8. Error Taxonomy
- AVEC_ERR_REQUIRED_FIELD
- AVEC_ERR_INVALID_RANGE
- AVEC_ERR_INVALID_ENUM
- AVEC_ERR_SCHEMA_VERSION
- AVEC_WARN_UNKNOWN_FIELD
- AVEC_WARN_PSI_MISMATCH
- AVEC_WARN_IMPUTED_LABEL

## 9. Versioning and Compatibility
1. Schema version format: avec-MAJOR.MINOR.
2. MAJOR may break compatibility.
3. MINOR must be additive.
4. New dimensions require MAJOR increment.

Compatibility contract:
1. Older valid AVEC artifacts remain parseable under newer MINOR versions.
2. Optional fields are never retroactively required.
3. Historical vectors are preserved without forced migration.

## 10. Claim Boundaries
operational:
- implemented and observable in running systems

empirically_supported:
- repeatedly observed and documented

hypothesis:
- coherent mechanism proposal pending deeper validation

## 11. Conformance
An implementation is AVEC v1.0 conformant if it:
1. Parses and validates AvecVector and envelope forms.
2. Implements strict and tolerant validation modes.
3. Emits defined errors and warnings.
4. Honors additive compatibility.
5. Preserves the distinction between knowledge retrieval and meaning retrieval.
