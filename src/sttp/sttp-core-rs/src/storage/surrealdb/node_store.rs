use std::sync::Arc;

use anyhow::Result;
use async_trait::async_trait;
use chrono::{DateTime, Utc};
use serde::de::DeserializeOwned;
use serde_json::json;
use uuid::Uuid;

use crate::domain::contracts::{NodeStore, NodeStoreInitializer};
use crate::domain::models::{AvecState, NodeQuery, SttpNode};
use crate::storage::surrealdb::client::{QueryParams, SurrealDbClient};
use crate::storage::surrealdb::models::{
    SurrealAvecRecord, SurrealNodeRecord, SurrealTriggerRecord,
};
use crate::storage::surrealdb::raw_queries;

pub struct SurrealDbNodeStore {
    client: Arc<dyn SurrealDbClient>,
}

impl SurrealDbNodeStore {
    pub fn new(client: Arc<dyn SurrealDbClient>) -> Self {
        Self { client }
    }
}

#[async_trait]
impl NodeStoreInitializer for SurrealDbNodeStore {
    async fn initialize_async(&self) -> Result<()> {
        self.client
            .raw_query(raw_queries::INIT_SCHEMA_QUERY, QueryParams::new())
            .await?;
        Ok(())
    }
}

#[async_trait]
impl NodeStore for SurrealDbNodeStore {
    async fn query_nodes_async(&self, query: NodeQuery) -> Result<Vec<SttpNode>> {
        let capped_limit = query.limit.max(1);
        let mut clauses = Vec::new();
        let mut parameters = QueryParams::new();

        if let Some(session_id) = query.session_id.as_ref().filter(|s| !s.trim().is_empty()) {
            clauses.push("session_id = $session_id".to_string());
            parameters.insert("session_id".to_string(), json!(session_id));
        }

        if let Some(from_utc) = query.from_utc {
            clauses.push("timestamp >= $from_utc".to_string());
            parameters.insert("from_utc".to_string(), json!(from_utc.to_rfc3339()));
        }

        if let Some(to_utc) = query.to_utc {
            clauses.push("timestamp <= $to_utc".to_string());
            parameters.insert("to_utc".to_string(), json!(to_utc.to_rfc3339()));
        }

        let where_clause = if clauses.is_empty() {
            String::new()
        } else {
            format!("WHERE {}", clauses.join(" AND "))
        };

        let query_text = raw_queries::query_nodes_query(&where_clause, capped_limit);
        let rows = self.client.raw_query(&query_text, parameters).await?;
        let records: Vec<SurrealNodeRecord> = decode_rows(rows)?;

        Ok(records.into_iter().map(map_to_node).collect())
    }

    async fn store_async(&self, node: SttpNode) -> Result<String> {
        let compression_avec_to_use = match node.compression_avec {
            Some(avec) if avec.psi() != 0.0 => avec,
            _ => node.model_avec,
        };

        let include_parent_assignment = node.parent_node_id.is_some();
        let mut parameters = QueryParams::new();

        parameters.insert("session_id".to_string(), json!(node.session_id));
        parameters.insert("raw".to_string(), json!(node.raw));
        parameters.insert("tier".to_string(), json!(node.tier));
        parameters.insert("timestamp".to_string(), json!(node.timestamp.to_rfc3339()));
        parameters.insert("compression_depth".to_string(), json!(node.compression_depth));
        parameters.insert("psi".to_string(), json!(node.psi));
        parameters.insert("rho".to_string(), json!(node.rho));
        parameters.insert("kappa".to_string(), json!(node.kappa));
        parameters.insert("user_stability".to_string(), json!(node.user_avec.stability));
        parameters.insert("user_friction".to_string(), json!(node.user_avec.friction));
        parameters.insert("user_logic".to_string(), json!(node.user_avec.logic));
        parameters.insert("user_autonomy".to_string(), json!(node.user_avec.autonomy));
        parameters.insert("user_psi".to_string(), json!(node.user_avec.psi()));
        parameters.insert("model_stability".to_string(), json!(node.model_avec.stability));
        parameters.insert("model_friction".to_string(), json!(node.model_avec.friction));
        parameters.insert("model_logic".to_string(), json!(node.model_avec.logic));
        parameters.insert("model_autonomy".to_string(), json!(node.model_avec.autonomy));
        parameters.insert("model_psi".to_string(), json!(node.model_avec.psi()));
        parameters.insert(
            "comp_stability".to_string(),
            json!(compression_avec_to_use.stability),
        );
        parameters.insert(
            "comp_friction".to_string(),
            json!(compression_avec_to_use.friction),
        );
        parameters.insert("comp_logic".to_string(), json!(compression_avec_to_use.logic));
        parameters.insert(
            "comp_autonomy".to_string(),
            json!(compression_avec_to_use.autonomy),
        );
        parameters.insert("comp_psi".to_string(), json!(compression_avec_to_use.psi()));

        if let Some(parent_node_id) = node.parent_node_id {
            parameters.insert("parent_node_id".to_string(), json!(parent_node_id));
        }

        let record_id = Uuid::new_v4().simple().to_string();
        let query_text =
            raw_queries::create_temporal_node_query(&record_id, include_parent_assignment);
        self.client.raw_query(&query_text, parameters).await?;

        Ok(record_id)
    }

    async fn get_by_resonance_async(
        &self,
        session_id: &str,
        current_avec: AvecState,
        limit: usize,
    ) -> Result<Vec<SttpNode>> {
        let query_text = raw_queries::get_by_resonance_query(current_avec.psi(), limit.max(1));
        let mut parameters = QueryParams::new();
        parameters.insert("session_id".to_string(), json!(session_id));

        let rows = self.client.raw_query(&query_text, parameters).await?;
        let records: Vec<SurrealNodeRecord> = decode_rows(rows)?;

        Ok(records.into_iter().map(map_to_node).collect())
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
        let mut parameters = QueryParams::new();
        parameters.insert("session_id".to_string(), json!(session_id));

        let rows = self
            .client
            .raw_query(raw_queries::GET_LAST_AVEC_QUERY, parameters)
            .await?;
        let records: Vec<SurrealAvecRecord> = decode_rows(rows)?;

        Ok(records.first().map(|last| AvecState {
            stability: last.stability,
            friction: last.friction,
            logic: last.logic,
            autonomy: last.autonomy,
        }))
    }

    async fn get_trigger_history_async(&self, session_id: &str) -> Result<Vec<String>> {
        let mut parameters = QueryParams::new();
        parameters.insert("session_id".to_string(), json!(session_id));

        let rows = self
            .client
            .raw_query(raw_queries::GET_TRIGGER_HISTORY_QUERY, parameters)
            .await?;
        let records: Vec<SurrealTriggerRecord> = decode_rows(rows)?;

        Ok(records.into_iter().map(|record| record.trigger).collect())
    }

    async fn store_calibration_async(
        &self,
        session_id: &str,
        avec: AvecState,
        trigger: &str,
    ) -> Result<()> {
        let mut parameters = QueryParams::new();
        parameters.insert("session_id".to_string(), json!(session_id));
        parameters.insert("stability".to_string(), json!(avec.stability));
        parameters.insert("friction".to_string(), json!(avec.friction));
        parameters.insert("logic".to_string(), json!(avec.logic));
        parameters.insert("autonomy".to_string(), json!(avec.autonomy));
        parameters.insert("psi".to_string(), json!(avec.psi()));
        parameters.insert("trigger".to_string(), json!(trigger));
        parameters.insert("created_at".to_string(), json!(Utc::now().to_rfc3339()));

        self.client
            .raw_query(raw_queries::STORE_CALIBRATION_QUERY, parameters)
            .await?;

        Ok(())
    }
}

fn map_to_node(record: SurrealNodeRecord) -> SttpNode {
    let _ = (record.user_psi, record.model_psi, record.comp_psi, record.resonance_delta);

    SttpNode {
        raw: record.raw,
        session_id: record.session_id,
        tier: record.tier,
        timestamp: parse_timestamp(&record.timestamp),
        compression_depth: record.compression_depth,
        parent_node_id: record.parent_node_id,
        psi: record.psi as f32,
        rho: record.rho as f32,
        kappa: record.kappa as f32,
        user_avec: AvecState {
            stability: record.user_stability as f32,
            friction: record.user_friction as f32,
            logic: record.user_logic as f32,
            autonomy: record.user_autonomy as f32,
        },
        model_avec: AvecState {
            stability: record.model_stability as f32,
            friction: record.model_friction as f32,
            logic: record.model_logic as f32,
            autonomy: record.model_autonomy as f32,
        },
        compression_avec: Some(AvecState {
            stability: record.comp_stability as f32,
            friction: record.comp_friction as f32,
            logic: record.comp_logic as f32,
            autonomy: record.comp_autonomy as f32,
        }),
    }
}

fn parse_timestamp(value: &str) -> DateTime<Utc> {
    DateTime::parse_from_rfc3339(value)
        .map(|dt| dt.with_timezone(&Utc))
        .unwrap_or_else(|_| Utc::now())
}

fn decode_rows<T>(rows: Vec<serde_json::Value>) -> Result<Vec<T>>
where
    T: DeserializeOwned,
{
    rows.into_iter()
        .map(serde_json::from_value)
        .collect::<std::result::Result<Vec<T>, _>>()
        .map_err(Into::into)
}
