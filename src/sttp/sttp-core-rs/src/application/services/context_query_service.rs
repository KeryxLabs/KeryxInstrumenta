use std::sync::Arc;

use anyhow::Result;

use crate::domain::contracts::NodeStore;
use crate::domain::models::{AvecState, ListNodesResult, PsiRange, RetrieveResult};

pub struct ContextQueryService {
    store: Arc<dyn NodeStore>,
}

impl ContextQueryService {
    pub fn new(store: Arc<dyn NodeStore>) -> Self {
        Self { store }
    }

    pub async fn get_context_async(
        &self,
        session_id: &str,
        stability: f32,
        friction: f32,
        logic: f32,
        autonomy: f32,
        limit: usize,
    ) -> RetrieveResult {
        let current = AvecState {
            stability,
            friction,
            logic,
            autonomy,
        };

        let nodes = match self
            .store
            .get_by_resonance_async(session_id, current, limit)
            .await
        {
            Ok(nodes) => nodes,
            Err(_) => return empty_retrieve_result(),
        };

        if nodes.is_empty() {
            return empty_retrieve_result();
        }

        let mut min = f32::INFINITY;
        let mut max = f32::NEG_INFINITY;
        let mut sum = 0.0f32;

        for node in &nodes {
            min = min.min(node.psi);
            max = max.max(node.psi);
            sum += node.psi;
        }

        let retrieved = nodes.len();

        RetrieveResult {
            retrieved,
            nodes,
            psi_range: PsiRange {
                min,
                max,
                average: sum / (retrieved as f32),
            },
        }
    }

    pub async fn list_nodes_async(&self, limit: usize, session_id: Option<&str>) -> Result<ListNodesResult> {
        let capped_limit = limit.clamp(1, 200);
        let nodes = self.store.list_nodes_async(capped_limit, session_id).await?;
        Ok(ListNodesResult {
            retrieved: nodes.len(),
            nodes,
        })
    }
}

fn empty_retrieve_result() -> RetrieveResult {
    RetrieveResult {
        nodes: Vec::new(),
        retrieved: 0,
        psi_range: PsiRange {
            min: 0.0,
            max: 0.0,
            average: 0.0,
        },
    }
}
