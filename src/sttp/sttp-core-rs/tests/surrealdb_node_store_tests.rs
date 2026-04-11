use std::collections::VecDeque;
use std::sync::Arc;

use anyhow::Result;
use async_trait::async_trait;
use chrono::{DateTime, Utc};
use serde_json::{Value, json};
use tokio::sync::Mutex;

use sttp_core_rs::domain::contracts::{NodeStore, NodeStoreInitializer};
use sttp_core_rs::domain::models::{AvecState, NodeQuery, SttpNode};
use sttp_core_rs::storage::surrealdb::{QueryParams, SurrealDbClient, SurrealDbNodeStore};

#[derive(Default)]
struct MockSurrealDbClient {
    responses: Mutex<VecDeque<Vec<Value>>>,
    queries: Mutex<Vec<String>>,
    parameters: Mutex<Vec<QueryParams>>,
}

impl MockSurrealDbClient {
    async fn queue_response(&self, rows: Vec<Value>) {
        self.responses.lock().await.push_back(rows);
    }

    async fn queries(&self) -> Vec<String> {
        self.queries.lock().await.clone()
    }

    async fn parameters(&self) -> Vec<QueryParams> {
        self.parameters.lock().await.clone()
    }
}

#[async_trait]
impl SurrealDbClient for MockSurrealDbClient {
    async fn raw_query(&self, query: &str, parameters: QueryParams) -> Result<Vec<Value>> {
        self.queries.lock().await.push(query.to_string());
        self.parameters.lock().await.push(parameters);
        Ok(self.responses.lock().await.pop_front().unwrap_or_default())
    }
}

#[tokio::test(flavor = "current_thread")]
async fn initialize_runs_schema_query() {
    let client = Arc::new(MockSurrealDbClient::default());
    let store = SurrealDbNodeStore::new(client.clone());

    store
        .initialize_async()
        .await
        .expect("schema initialization should succeed");

    let queries = client.queries().await;
    assert_eq!(queries.len(), 3);
    assert!(queries[0].contains("DEFINE TABLE IF NOT EXISTS temporal_node"));
    assert!(queries[0].contains("DEFINE FIELD IF NOT EXISTS tenant_id"));
    assert!(queries[1].contains("FROM temporal_node"));
    assert!(queries[2].contains("FROM calibration"));
}

#[tokio::test(flavor = "current_thread")]
async fn initialize_backfills_tenant_ids_for_legacy_rows() {
    let client = Arc::new(MockSurrealDbClient::default());

    client.queue_response(vec![]).await;

    client
        .queue_response(vec![json!({
            "id": "temporal_node:legacy_node",
            "session_id": "tenant:acme::session:alpha"
        })])
        .await;

    client.queue_response(vec![]).await;

    client
        .queue_response(vec![json!({
            "id": "calibration:legacy_cal",
            "session_id": "legacy-session"
        })])
        .await;

    client.queue_response(vec![]).await;

    let store = SurrealDbNodeStore::new(client.clone());
    store
        .initialize_async()
        .await
        .expect("schema initialization should succeed");

    let queries = client.queries().await;
    assert!(queries
        .iter()
        .any(|query| query.contains("UPDATE temporal_node:legacy_node")));
    assert!(queries
        .iter()
        .any(|query| query.contains("UPDATE calibration:legacy_cal")));

    let params = client.parameters().await;
    assert!(params
        .iter()
        .any(|param| param.get("tenant_id") == Some(&json!("acme"))));
    assert!(params
        .iter()
        .any(|param| param.get("tenant_id") == Some(&json!("default"))));
}

#[tokio::test(flavor = "current_thread")]
async fn query_nodes_maps_result_rows_to_domain_nodes() {
    let client = Arc::new(MockSurrealDbClient::default());
    client
        .queue_response(vec![json!({
            "SessionId": "s1",
            "Raw": "node raw",
            "Tier": "raw",
            "Timestamp": "2026-03-05T06:30:00Z",
            "CompressionDepth": 1,
            "ParentNodeId": null,
            "Psi": 2.6,
            "Rho": 0.96,
            "Kappa": 0.94,
            "UserStability": 0.85,
            "UserFriction": 0.25,
            "UserLogic": 0.80,
            "UserAutonomy": 0.70,
            "UserPsi": 2.60,
            "ModelStability": 0.85,
            "ModelFriction": 0.25,
            "ModelLogic": 0.80,
            "ModelAutonomy": 0.70,
            "ModelPsi": 2.60,
            "CompStability": 0.85,
            "CompFriction": 0.25,
            "CompLogic": 0.80,
            "CompAutonomy": 0.70,
            "CompPsi": 2.60,
            "ResonanceDelta": 0.0
        })])
        .await;

    let store = SurrealDbNodeStore::new(client.clone());

    let nodes = store
        .query_nodes_async(NodeQuery {
            limit: 5,
            session_id: Some("s1".to_string()),
            from_utc: None,
            to_utc: None,
        })
        .await
        .expect("query should succeed");

    assert_eq!(nodes.len(), 1);
    assert_eq!(nodes[0].session_id, "s1");
    assert_eq!(nodes[0].tier, "raw");
    assert!((nodes[0].rho - 0.96).abs() <= 0.0001);

    let queries = client.queries().await;
    assert_eq!(queries.len(), 1);
    assert!(queries[0].contains("tenant_id = $tenant_id OR tenant_id = NONE OR tenant_id = ''"));
    assert!(queries[0].contains("session_id = $session_id"));

    let params = client.parameters().await;
    assert_eq!(params.len(), 1);
    assert_eq!(
        params[0]
            .get("tenant_id")
            .expect("tenant_id should be present for session-scoped query"),
        &json!("default")
    );
}

#[tokio::test(flavor = "current_thread")]
async fn store_uses_model_avec_when_compression_avec_is_zero() {
    let client = Arc::new(MockSurrealDbClient::default());
    let store = SurrealDbNodeStore::new(client.clone());

    let node = SttpNode {
        raw: "raw".to_string(),
        session_id: "session".to_string(),
        tier: "raw".to_string(),
        timestamp: DateTime::parse_from_rfc3339("2026-03-05T06:30:00Z")
            .expect("timestamp should parse")
            .with_timezone(&Utc),
        compression_depth: 1,
        parent_node_id: None,
        user_avec: AvecState {
            stability: 0.85,
            friction: 0.25,
            logic: 0.80,
            autonomy: 0.70,
        },
        model_avec: AvecState {
            stability: 0.91,
            friction: 0.21,
            logic: 0.90,
            autonomy: 0.80,
        },
        compression_avec: Some(AvecState::zero()),
        rho: 0.96,
        kappa: 0.94,
        psi: 2.6,
    };

    let node_id = store
        .store_async(node)
        .await
        .expect("store should succeed");
    assert!(!node_id.trim().is_empty());

    let params = client.parameters().await;
    assert_eq!(params.len(), 1);

    let comp_stability = params[0]
        .get("comp_stability")
        .expect("comp_stability must be present")
        .as_f64()
        .expect("comp_stability must be numeric");
    assert!((comp_stability - 0.91).abs() <= 0.0001);
    assert_eq!(
        params[0]
            .get("tenant_id")
            .expect("tenant_id must be present"),
        &json!("default")
    );
}

#[tokio::test(flavor = "current_thread")]
async fn store_derives_tenant_id_from_scoped_session_key() {
    let client = Arc::new(MockSurrealDbClient::default());
    let store = SurrealDbNodeStore::new(client.clone());

    let node = SttpNode {
        raw: "raw".to_string(),
        session_id: "tenant:acme::session:session-42".to_string(),
        tier: "raw".to_string(),
        timestamp: DateTime::parse_from_rfc3339("2026-03-05T06:30:00Z")
            .expect("timestamp should parse")
            .with_timezone(&Utc),
        compression_depth: 1,
        parent_node_id: None,
        user_avec: AvecState {
            stability: 0.85,
            friction: 0.25,
            logic: 0.80,
            autonomy: 0.70,
        },
        model_avec: AvecState {
            stability: 0.91,
            friction: 0.21,
            logic: 0.90,
            autonomy: 0.80,
        },
        compression_avec: Some(AvecState::zero()),
        rho: 0.96,
        kappa: 0.94,
        psi: 2.6,
    };

    store.store_async(node).await.expect("store should succeed");

    let params = client.parameters().await;
    assert_eq!(params.len(), 1);
    assert_eq!(
        params[0]
            .get("tenant_id")
            .expect("tenant_id must be present"),
        &json!("acme")
    );
}
