use anyhow::Result;
use async_trait::async_trait;
use tokio::sync::RwLock;
use uuid::Uuid;

use crate::domain::contracts::{NodeStore, NodeStoreInitializer};
use crate::domain::models::{AvecState, NodeQuery, SttpNode};

#[derive(Debug, Clone, Copy)]
struct CalibrationRecord {
    avec: AvecState,
}

#[derive(Debug, Default)]
pub struct InMemoryNodeStore {
    nodes: RwLock<Vec<SttpNode>>,
    calibrations: RwLock<Vec<(String, CalibrationRecord, String)>>,
}

impl InMemoryNodeStore {
    pub fn new() -> Self {
        Self::default()
    }
}

#[async_trait]
impl NodeStoreInitializer for InMemoryNodeStore {
    async fn initialize_async(&self) -> Result<()> {
        Ok(())
    }
}

#[async_trait]
impl NodeStore for InMemoryNodeStore {
    async fn query_nodes_async(&self, query: NodeQuery) -> Result<Vec<SttpNode>> {
        let capped_limit = query.limit.max(1);
        let nodes = self.nodes.read().await;

        let mut result = nodes
            .iter()
            .filter(|n| {
                query
                    .session_id
                    .as_ref()
                    .map(|s| &n.session_id == s)
                    .unwrap_or(true)
            })
            .filter(|n| query.from_utc.map(|from| n.timestamp >= from).unwrap_or(true))
            .filter(|n| query.to_utc.map(|to| n.timestamp <= to).unwrap_or(true))
            .cloned()
            .collect::<Vec<_>>();

        result.sort_by(|a, b| b.timestamp.cmp(&a.timestamp));
        result.truncate(capped_limit);

        Ok(result)
    }

    async fn store_async(&self, node: SttpNode) -> Result<String> {
        let mut nodes = self.nodes.write().await;
        nodes.push(node);
        Ok(Uuid::new_v4().to_string())
    }

    async fn get_by_resonance_async(
        &self,
        session_id: &str,
        current_avec: AvecState,
        limit: usize,
    ) -> Result<Vec<SttpNode>> {
        let nodes = self.nodes.read().await;

        let mut result = nodes
            .iter()
            .filter(|n| n.session_id == session_id)
            .cloned()
            .collect::<Vec<_>>();

        result.sort_by(|a, b| {
            let ad = (a.psi - current_avec.psi()).abs();
            let bd = (b.psi - current_avec.psi()).abs();
            ad.total_cmp(&bd)
        });
        result.truncate(limit);

        Ok(result)
    }

    async fn list_nodes_async(&self, limit: usize, session_id: Option<&str>) -> Result<Vec<SttpNode>> {
        self.query_nodes_async(NodeQuery {
            limit: limit.clamp(1, 200),
            session_id: session_id.map(|s| s.to_string()),
            from_utc: None,
            to_utc: None,
        })
        .await
    }

    async fn get_last_avec_async(&self, session_id: &str) -> Result<Option<AvecState>> {
        let calibrations = self.calibrations.read().await;
        let last = calibrations
            .iter()
            .rev()
            .find(|(sid, _, _)| sid == session_id)
            .map(|(_, record, _)| record.avec);
        Ok(last)
    }

    async fn get_trigger_history_async(&self, session_id: &str) -> Result<Vec<String>> {
        let calibrations = self.calibrations.read().await;
        let history = calibrations
            .iter()
            .filter(|(sid, _, _)| sid == session_id)
            .map(|(_, _, trigger)| trigger.clone())
            .collect::<Vec<_>>();

        Ok(history)
    }

    async fn store_calibration_async(
        &self,
        session_id: &str,
        avec: AvecState,
        trigger: &str,
    ) -> Result<()> {
        let mut calibrations = self.calibrations.write().await;
        calibrations.push((
            session_id.to_string(),
            CalibrationRecord { avec },
            trigger.to_string(),
        ));
        Ok(())
    }
}
