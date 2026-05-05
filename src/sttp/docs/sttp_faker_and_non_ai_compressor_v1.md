# STTP Faker Builder and Non-AI Compressor v1

Date: 2026-05-03
Status: Draft v1

## 1. Goal

Define a deterministic, reproducible pipeline for:

1. generating realistic synthetic STTP-like text payloads for load and quality testing,
2. compressing raw text into anchor-centric structured content without any AI model calls.

This track is intended to complement, not replace, AI-assisted compression and scoring.

## 2. Why This Matters

1. Enables high-volume test data generation without model cost.
2. Gives a deterministic baseline for recall and ranking comparisons.
3. Improves failure isolation by separating language model variance from retrieval logic.
4. Supports offline and low-resource deployments.

## 3. Scope

In scope for v1:

1. STTP faker builder for synthetic corpus generation.
2. Non-AI compressor that produces anchor_topic and compact key points.
3. Deterministic scoring and top-k anchor extraction.
4. Benchmark harness against existing retrieval paths.

Out of scope for v1:

1. Full STTP node synthesis with perfect semantic realism.
2. AVEC inference from raw text.
3. Model-based summarization.
4. Replacing current store_context parser validation rules.

## 4. Deliverables

1. Faker module with scenario config and deterministic random seed support.
2. Compressor module with stable tokenization, filtering, ranking, and output schema.
3. Test fixtures and conformance tests for deterministic outputs.
4. Benchmark report comparing raw-only, compressed-only, and hybrid retrieval.

## 5. Data Contracts

### 5.1 Faker Input

FakerConfig:

1. seed: u64
2. sessions: usize
3. nodes_per_session: Range
4. tier_distribution: map raw/daily/weekly/monthly
5. filler_ratio: float [0,1]
6. topic_drift: float [0,1]
7. timestamp_span_days: usize
8. domain_lexicon: list of weighted topic terms

### 5.2 Faker Output

FakerOutputRecord:

1. synthetic_id: string
2. session_id: string
3. tier: string
4. timestamp: datetime
5. raw_text: string
6. expected_anchor_terms: list<string>
7. noise_profile:
   1. filler_ratio_actual
   2. distractor_ratio

### 5.3 Compressor Input

CompressorRequest:

1. text: string
2. max_anchors: usize (default 5)
3. max_points: usize (default 5)
4. min_token_length: usize (default 3)
5. stopword_profile: enum basic|extended|domain
6. phrase_mode: enum none|rake_lite|textrank_lite

### 5.4 Compressor Output

CompressorResult:

1. anchor_topic: string
2. anchor_terms: list of AnchorTerm
3. key_points: list<string>
4. salient_phrases: list<string>
5. compression_ratio: float
6. discarded_noise_ratio: float
7. diagnostics:
   1. tokens_total
   2. tokens_kept
   3. stopwords_removed
   4. filler_removed
   5. sentences_total

AnchorTerm:

1. term: string
2. score: float
3. evidence_count: usize
4. first_position: usize

## 6. Deterministic Compression Pipeline

Stage A: Normalize

1. lowercase
2. normalize unicode whitespace
3. remove punctuation except intra-word separators
4. preserve numeric tokens and known technical tokens

Stage B: Tokenize

1. whitespace tokenization with simple boundary rules
2. optional phrase candidate extraction (n-grams 1..3)

Stage C: Filter

1. remove stopwords by profile
2. remove filler words and discourse markers
3. remove tokens shorter than min_token_length unless allowlisted

Stage D: Score

Each candidate term gets:

score(term) = w_f * freq_norm + w_p * pos_boost + w_r * rarity_boost + w_c * cooccur_centrality

Default weights:

1. w_f = 0.45
2. w_p = 0.20
3. w_r = 0.20
4. w_c = 0.15

Where:

1. freq_norm: normalized frequency in filtered text
2. pos_boost: early appearance boost
3. rarity_boost: inverse document frequency proxy from local corpus stats
4. cooccur_centrality: term co-occurrence degree in sentence graph

Stage E: Select

1. choose top-k anchors (default 5)
2. choose anchor_topic as highest scored anchor or highest scored phrase containing top anchor

Stage F: Key Point Construction

1. rank sentences by anchor density and coverage
2. keep top max_points sentences
3. apply deterministic compression heuristics:
   1. remove low-information clauses
   2. preserve entities, numbers, and action verbs

## 7. Filler and Noise Handling

v1 filler lexicon includes discourse-heavy terms such as:

1. basically
2. literally
3. actually
4. just
5. really
6. kind of
7. sort of
8. you know
9. i mean

Rules:

1. remove filler tokens only when outside allowlisted technical phrases.
2. keep repeated filler count for discarded_noise_ratio metrics.

## 8. Quality Metrics

Per-record metrics:

1. anchor_hit_rate: overlap between expected_anchor_terms and anchor_terms
2. point_coverage: fraction of anchor terms represented in key_points
3. compression_ratio: output_tokens / input_tokens
4. stability_score: output equality across repeated runs with same input and config

Corpus metrics:

1. p50 and p95 latency
2. memory usage per 1k records
3. retrieval uplift delta versus raw-only indexing

## 9. Benchmark Plan

Compare three indexing modes:

1. RawOnly: index raw text only.
2. CompressedOnly: index compressor output only.
3. Hybrid: index both raw and compressed fields.

Evaluation queries:

1. exact anchor matches
2. paraphrase-like keyword sets (non-AI generated variants)
3. noisy/filler-heavy queries

Success criteria for v1:

1. deterministic output equality for same input and config at 100 percent.
2. compressor latency under 5 ms p95 on reference hardware for medium records.
3. hybrid retrieval recall at least equal to raw-only baseline.
4. compressed-only mode within acceptable recall threshold for top-5 retrieval.

## 10. Integration Path

Step 1:

1. implement faker in SDK test support module.
2. generate fixtures for memory_find and memory_recall integration tests.

Step 2:

1. implement compressor library module in sttp-sdk-rs application layer.
2. expose as deterministic helper API, not a transport endpoint yet.

Step 3:

1. add optional pre-processing hook in memory_write and memory_transform recipes.
2. keep disabled by default behind explicit config.

Step 4:

1. add benchmark command for corpus generation and retrieval comparison.
2. publish benchmark summary in docs.

## 11. Risk Notes

1. Over-aggressive filtering can suppress domain-critical terms.
2. Phrase extraction can overfit repeated boilerplate.
3. Corpus rarity boosts can become unstable on very small datasets.

Mitigations:

1. domain allowlists and deny lists
2. conservative defaults
3. deterministic diagnostics output for every run

## 12. Next Sprint Tasks

Sprint A:

1. FakerConfig and FakerOutputRecord contracts.
2. seeded generator implementation.
3. fixture export utility.

Sprint B:

1. compressor pipeline stages A through E.
2. anchor ranking tests and determinism tests.

Sprint C:

1. key point construction stage.
2. benchmark harness and report template.

Sprint D:

1. optional integration hooks into write and transform flows.
2. A/B retrieval validation in test DB.
