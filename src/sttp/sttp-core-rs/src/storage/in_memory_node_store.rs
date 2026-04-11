use anyhow::{Result, anyhow};
use async_trait::async_trait;
use tokio::sync::RwLock;
use uuid::Uuid;

use crate::domain::contracts::{NodeStore, NodeStoreInitializer};
use crate::domain::models::{
    AvecState, BatchRekeyResult, NodeQuery, ScopeRekeyResult, SttpNode,
};

const DEFAULT_TENANT: &str = "default";
const TENANT_SCOPE_PREFIX: &str = "tenant:";
const TENANT_SCOPE_SEPARATOR: &str = "::session:";

#[derive(Debug, Clone, Copy)]
struct CalibrationRecord {
    avec: AvecState,
}

#[derive(Debug, Default)]
pub struct InMemoryNodeStore {
    nodes: RwLock<Vec<(String, SttpNode)>>,
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
            .map(|(_, node)| node)
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
        let node_id = Uuid::new_v4().to_string();
        nodes.push((node_id.clone(), node));
        Ok(node_id)
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
            .map(|(_, node)| node)
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

    async fn batch_rekey_scopes_async(
        &self,
        node_ids: Vec<String>,
        target_tenant_id: &str,
        target_session_id: &str,
        dry_run: bool,
        allow_merge: bool,
    ) -> Result<BatchRekeyResult> {
        if node_ids.is_empty() {
            return Err(anyhow!("at least one node id is required"));
        }

        let target_tenant_id = normalize_tenant_id(target_tenant_id);
        if target_session_id.trim().is_empty() {
            return Err(anyhow!("target session id cannot be empty"));
        }

        let normalized_node_ids = node_ids
            .into_iter()
            .filter_map(|node_id| normalize_temporal_node_id(&node_id))
            .collect::<Vec<_>>();

        if normalized_node_ids.is_empty() {
            return Err(anyhow!("no valid node ids were provided"));
        }

        let nodes_snapshot = self.nodes.read().await.clone();
        let calibrations_snapshot = self.calibrations.read().await.clone();

        let mut missing_node_ids = Vec::new();
        let mut scopes = Vec::new();

        for node_id in &normalized_node_ids {
            let Some((_, node)) = nodes_snapshot.iter().find(|(id, _)| id == node_id) else {
                missing_node_ids.push(node_id.clone());
                continue;
            };

            let tenant_id = derive_tenant_id_from_session(&node.session_id);
            if !scopes
                .iter()
                .any(|(tenant, session): &(String, String)| tenant == &tenant_id && session == &node.session_id)
            {
                scopes.push((tenant_id, node.session_id.clone()));
            }
        }

        let mut scope_results = Vec::new();
        let mut temporal_nodes_updated = 0usize;
        let mut calibrations_updated = 0usize;
        let mut sources_to_apply = Vec::new();

        for (source_tenant_id, source_session_id) in scopes {
            let same_scope = source_tenant_id == target_tenant_id && source_session_id == target_session_id;

            let temporal_nodes = nodes_snapshot
                .iter()
                .filter(|(_, node)| node.session_id == source_session_id)
                .count();
            let calibrations = calibrations_snapshot
                .iter()
                .filter(|(session_id, _, _)| session_id == &source_session_id)
                .count();

            let target_temporal_nodes = if same_scope {
                0
            } else {
                nodes_snapshot
                    .iter()
                    .filter(|(_, node)| node.session_id == target_session_id)
                    .count()
            };
            let target_calibrations = if same_scope {
                0
            } else {
                calibrations_snapshot
                    .iter()
                    .filter(|(session_id, _, _)| session_id == target_session_id)
                    .count()
            };

            let conflict = !allow_merge
                && !same_scope
                && (target_temporal_nodes > 0 || target_calibrations > 0);

            let mut applied = false;
            let message = if same_scope {
                Some("source and target scopes are identical".to_string())
            } else if conflict {
                Some("target scope already contains rows; set allow_merge=true to override"
                    .to_string())
            } else {
                if !dry_run {
                    applied = true;
                    temporal_nodes_updated += temporal_nodes;
                    calibrations_updated += calibrations;
                    sources_to_apply.push(source_session_id.clone());
                }
                None
            };

            scope_results.push(ScopeRekeyResult {
                source_tenant_id,
                source_session_id,
                target_tenant_id: target_tenant_id.clone(),
                target_session_id: target_session_id.to_string(),
                temporal_nodes,
                calibrations,
                target_temporal_nodes,
                target_calibrations,
                applied,
                conflict,
                message,
            });
        }

        if !dry_run && !sources_to_apply.is_empty() {
            let mut nodes = self.nodes.write().await;
            for (_, node) in nodes.iter_mut() {
                if sources_to_apply.iter().any(|source| source == &node.session_id) {
                    node.session_id = target_session_id.to_string();
                }
            }

            let mut calibrations = self.calibrations.write().await;
            for (session_id, _, _) in calibrations.iter_mut() {
                if sources_to_apply.iter().any(|source| source == session_id) {
                    *session_id = target_session_id.to_string();
                }
            }
        }

        Ok(BatchRekeyResult {
            dry_run,
            requested_node_ids: normalized_node_ids.len(),
            resolved_node_ids: normalized_node_ids.len().saturating_sub(missing_node_ids.len()),
            missing_node_ids,
            scopes: scope_results,
            temporal_nodes_updated,
            calibrations_updated,
        })
    }
}

fn parse_scoped_session_id(session_id: &str) -> Option<(&str, &str)> {
    let remainder = session_id.strip_prefix(TENANT_SCOPE_PREFIX)?;
    remainder.split_once(TENANT_SCOPE_SEPARATOR)
}

fn derive_tenant_id_from_session(session_id: &str) -> String {
    parse_scoped_session_id(session_id)
        .map(|(tenant, _)| tenant)
        .filter(|tenant| !tenant.trim().is_empty())
        .unwrap_or(DEFAULT_TENANT)
        .to_string()
}

fn normalize_tenant_id(value: &str) -> String {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        DEFAULT_TENANT.to_string()
    } else {
        trimmed.to_string()
    }
}

fn normalize_temporal_node_id(value: &str) -> Option<String> {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        return None;
    }

    if let Some((table, id)) = trimmed.split_once(':') {
        if table != "temporal_node" {
            return None;
        }

        let id = id.trim();
        if id.is_empty() {
            None
        } else {
            Some(id.to_string())
        }
    } else {
        Some(trimmed.to_string())
    }
}
