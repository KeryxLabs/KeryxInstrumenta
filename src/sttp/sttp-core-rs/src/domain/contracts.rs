use anyhow::Result;
use async_trait::async_trait;

use crate::domain::models::{
    AvecState, BatchRekeyResult, NodeQuery, SttpNode, ValidationResult,
};

#[async_trait]
pub trait NodeStore: Send + Sync {
    async fn query_nodes_async(&self, query: NodeQuery) -> Result<Vec<SttpNode>>;

    async fn store_async(&self, node: SttpNode) -> Result<String>;

    async fn get_by_resonance_async(
        &self,
        session_id: &str,
        current_avec: AvecState,
        limit: usize,
    ) -> Result<Vec<SttpNode>>;

    async fn list_nodes_async(
        &self,
        limit: usize,
        session_id: Option<&str>,
    ) -> Result<Vec<SttpNode>>;

    async fn get_last_avec_async(&self, session_id: &str) -> Result<Option<AvecState>>;

    async fn get_trigger_history_async(&self, session_id: &str) -> Result<Vec<String>>;

    async fn store_calibration_async(
        &self,
        session_id: &str,
        avec: AvecState,
        trigger: &str,
    ) -> Result<()>;

    async fn batch_rekey_scopes_async(
        &self,
        node_ids: Vec<String>,
        target_tenant_id: &str,
        target_session_id: &str,
        dry_run: bool,
        allow_merge: bool,
    ) -> Result<BatchRekeyResult>;
}

#[async_trait]
pub trait NodeStoreInitializer: Send + Sync {
    async fn initialize_async(&self) -> Result<()>;
}

pub trait NodeValidator: Send + Sync {
    fn validate(&self, raw_node: &str) -> ValidationResult;
    fn verify_psi(&self, node: &SttpNode) -> bool;
}
