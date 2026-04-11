use anyhow::Result;
use async_trait::async_trait;

use crate::domain::models::{
    AvecState, BatchRekeyResult, NodeQuery, SttpNode, ValidationResult,
};

/// Storage abstraction for STTP nodes and calibration data.
///
/// Implementors are expected to preserve semantics across both in-memory and
/// persistent backends.
#[async_trait]
pub trait NodeStore: Send + Sync {
    /// Query nodes with optional session and time filters.
    async fn query_nodes_async(&self, query: NodeQuery) -> Result<Vec<SttpNode>>;

    /// Persist a parsed node and return its storage identifier.
    async fn store_async(&self, node: SttpNode) -> Result<String>;

    /// Retrieve nodes ordered by resonance to the provided AVEC state.
    async fn get_by_resonance_async(
        &self,
        session_id: &str,
        current_avec: AvecState,
        limit: usize,
    ) -> Result<Vec<SttpNode>>;

    /// List recent nodes with an optional session filter.
    async fn list_nodes_async(
        &self,
        limit: usize,
        session_id: Option<&str>,
    ) -> Result<Vec<SttpNode>>;

    /// Read the most recent calibration AVEC for a session.
    async fn get_last_avec_async(&self, session_id: &str) -> Result<Option<AvecState>>;

    /// Read calibration trigger history for a session.
    async fn get_trigger_history_async(&self, session_id: &str) -> Result<Vec<String>>;

    /// Store a new calibration measurement for a session.
    async fn store_calibration_async(
        &self,
        session_id: &str,
        avec: AvecState,
        trigger: &str,
    ) -> Result<()>;

    /// Batch-rekey one or more source scopes to a target scope using node IDs as anchors.
    ///
    /// Implementations should treat `node_ids` as source-scope anchors and apply scope-wide
    /// updates across all related tables, not just the anchor records themselves.
    async fn batch_rekey_scopes_async(
        &self,
        node_ids: Vec<String>,
        target_tenant_id: &str,
        target_session_id: &str,
        dry_run: bool,
        allow_merge: bool,
    ) -> Result<BatchRekeyResult>;
}

/// One-time initializer contract for a storage backend.
///
/// This is typically used for schema creation and migration/backfill hooks.
#[async_trait]
pub trait NodeStoreInitializer: Send + Sync {
    async fn initialize_async(&self) -> Result<()>;
}

/// Validator contract for raw STTP node payloads.
pub trait NodeValidator: Send + Sync {
    /// Validate structural and semantic correctness of raw STTP text.
    fn validate(&self, raw_node: &str) -> ValidationResult;
    /// Verify PSI coherence between fields and computed values.
    fn verify_psi(&self, node: &SttpNode) -> bool;
}
