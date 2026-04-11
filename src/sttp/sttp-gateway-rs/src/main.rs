use std::collections::{BTreeMap, HashMap};
use std::net::SocketAddr;
use std::sync::Arc;

use anyhow::{Result, anyhow};
use axum::extract::{Query, State};
use axum::http::StatusCode;
use axum::routing::{get, post};
use axum::{Json, Router};
use chrono::{DateTime, Utc};
use clap::Parser;
use serde::{Deserialize, Serialize};
use serde_json::{Value, json};
use tokio::net::TcpListener;
use tonic::transport::Server;
use tonic::{Request, Response, Status};
use tracing::{error, info};
use tracing_subscriber::EnvFilter;

use sttp_core_rs::application::services::{
    CalibrationService, ContextQueryService, MonthlyRollupService, MoodCatalogService,
    StoreContextService,
};
use sttp_core_rs::application::validation::TreeSitterValidator;
use sttp_core_rs::domain::contracts::{NodeStore, NodeStoreInitializer, NodeValidator};
use sttp_core_rs::domain::models::{
    self as core_models, ConfidenceBandSummary, MonthlyRollupRequest, NumericRange, PsiRange,
};
use sttp_core_rs::storage::InMemoryNodeStore;

pub mod proto {
    tonic::include_proto!("sttp.v1");
}

const FILE_DESCRIPTOR_SET: &[u8] = tonic::include_file_descriptor_set!("sttp_descriptor");

#[derive(Parser, Debug)]
#[command(author, version, about)]
struct GatewayArgs {
    #[arg(long, env = "STTP_GATEWAY_HTTP_PORT", default_value_t = 8080)]
    http_port: u16,

    #[arg(long, env = "STTP_GATEWAY_GRPC_PORT", default_value_t = 8081)]
    grpc_port: u16,
}

#[derive(Clone)]
struct AppState {
    calibration: Arc<CalibrationService>,
    context_query: Arc<ContextQueryService>,
    mood_catalog: Arc<MoodCatalogService>,
    store_context: Arc<StoreContextService>,
    monthly_rollup: Arc<MonthlyRollupService>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ErrorResponse {
    error: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CalibrateSessionHttpRequest {
    session_id: String,
    stability: f32,
    friction: f32,
    logic: f32,
    autonomy: f32,
    trigger: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct StoreContextHttpRequest {
    node: String,
    session_id: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct GetContextHttpRequest {
    session_id: String,
    stability: f32,
    friction: f32,
    logic: f32,
    autonomy: f32,
    limit: Option<usize>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CreateMonthlyRollupHttpRequest {
    session_id: String,
    start_date_utc: DateTime<Utc>,
    end_date_utc: DateTime<Utc>,
    source_session_id: Option<String>,
    parent_node_id: Option<String>,
    persist: Option<bool>,
    limit: Option<usize>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ListNodesQuery {
    limit: Option<usize>,
    session_id: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct GetMoodsQuery {
    target_mood: Option<String>,
    blend: Option<f32>,
    current_stability: Option<f32>,
    current_friction: Option<f32>,
    current_logic: Option<f32>,
    current_autonomy: Option<f32>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct GraphQuery {
    limit: Option<usize>,
    session_id: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct AvecStateDto {
    stability: f32,
    friction: f32,
    logic: f32,
    autonomy: f32,
    psi: f32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct SttpNodeDto {
    raw: String,
    session_id: String,
    tier: String,
    timestamp: DateTime<Utc>,
    compression_depth: i32,
    parent_node_id: Option<String>,
    user_avec: AvecStateDto,
    model_avec: AvecStateDto,
    compression_avec: Option<AvecStateDto>,
    rho: f32,
    kappa: f32,
    psi: f32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct PsiRangeDto {
    min: f32,
    max: f32,
    average: f32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct NumericRangeDto {
    min: f32,
    max: f32,
    average: f32,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ConfidenceBandSummaryDto {
    low: usize,
    medium: usize,
    high: usize,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct CalibrationResultDto {
    previous_avec: AvecStateDto,
    delta: f32,
    drift_classification: String,
    trigger: String,
    trigger_history: Vec<String>,
    is_first_calibration: bool,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct StoreResultDto {
    node_id: String,
    psi: f32,
    valid: bool,
    validation_error: Option<String>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct RetrieveResultDto {
    nodes: Vec<SttpNodeDto>,
    retrieved: usize,
    psi_range: PsiRangeDto,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct ListNodesResultDto {
    nodes: Vec<SttpNodeDto>,
    retrieved: usize,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MoodPresetDto {
    name: String,
    description: String,
    avec: AvecStateDto,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MoodSwapPreviewDto {
    target_mood: String,
    blend: f32,
    current: AvecStateDto,
    target: AvecStateDto,
    blended: AvecStateDto,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MoodCatalogResultDto {
    presets: Vec<MoodPresetDto>,
    apply_guide: String,
    swap_preview: Option<MoodSwapPreviewDto>,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
struct MonthlyRollupResultDto {
    success: bool,
    node_id: String,
    raw_node: String,
    error: Option<String>,
    source_nodes: usize,
    parent_reference: Option<String>,
    user_average: AvecStateDto,
    model_average: AvecStateDto,
    compression_average: AvecStateDto,
    rho_range: NumericRangeDto,
    kappa_range: NumericRangeDto,
    psi_range: NumericRangeDto,
    rho_bands: ConfidenceBandSummaryDto,
    kappa_bands: ConfidenceBandSummaryDto,
}

#[derive(Debug, Serialize)]
struct GraphResponse {
    sessions: Vec<Value>,
    nodes: Vec<Value>,
    edges: Vec<Value>,
    retrieved: usize,
}

type ApiResult<T> = Result<Json<T>, (StatusCode, Json<ErrorResponse>)>;

#[tokio::main]
async fn main() -> Result<()> {
    tracing_subscriber::fmt()
        .with_env_filter(EnvFilter::from_default_env())
        .init();

    let args = GatewayArgs::parse();

    if args.http_port == args.grpc_port {
        return Err(anyhow!(
            "--http-port and --grpc-port must be different values"
        ));
    }

    let state = Arc::new(build_state().await?);

    let http_router = Router::new()
        .route("/health", get(health_handler))
        .route("/api/v1/calibrate", post(calibrate_handler))
        .route("/api/v1/store", post(store_context_handler))
        .route("/api/v1/context", post(get_context_handler))
        .route("/api/v1/nodes", get(list_nodes_handler))
        .route("/api/v1/graph", get(graph_handler))
        .route("/api/v1/moods", get(get_moods_handler))
        .route("/api/v1/rollups/monthly", post(create_monthly_rollup_handler))
        .with_state(state.clone());

    let grpc_service = GrpcGatewayService::new(state);

    let grpc_addr = SocketAddr::from(([0, 0, 0, 0], args.grpc_port));
    let http_listener = TcpListener::bind(("0.0.0.0", args.http_port)).await?;

    let reflection_service = tonic_reflection::server::Builder::configure()
        .register_encoded_file_descriptor_set(FILE_DESCRIPTOR_SET)
        .build_v1()?;

    info!(
        http_port = args.http_port,
        grpc_port = args.grpc_port,
        "Starting sttp-gateway-rs"
    );

    let http_server = axum::serve(http_listener, http_router).with_graceful_shutdown(shutdown_signal());
    let grpc_server = Server::builder()
        .add_service(reflection_service)
        .add_service(proto::sttp_gateway_service_server::SttpGatewayServiceServer::new(
            grpc_service,
        ))
        .serve_with_shutdown(grpc_addr, shutdown_signal());

    let (http_result, grpc_result) = tokio::join!(http_server, grpc_server);
    if let Err(err) = http_result {
        error!(error = %err, "HTTP server exited with error");
        return Err(err.into());
    }
    if let Err(err) = grpc_result {
        error!(error = %err, "gRPC server exited with error");
        return Err(err.into());
    }

    Ok(())
}

async fn build_state() -> Result<AppState> {
    let store = Arc::new(InMemoryNodeStore::new());

    let initializer: Arc<dyn NodeStoreInitializer> = store.clone();
    initializer.initialize_async().await?;

    let store_trait: Arc<dyn NodeStore> = store;
    let validator: Arc<dyn NodeValidator> = Arc::new(TreeSitterValidator);

    Ok(AppState {
        calibration: Arc::new(CalibrationService::new(store_trait.clone())),
        context_query: Arc::new(ContextQueryService::new(store_trait.clone())),
        mood_catalog: Arc::new(MoodCatalogService::new()),
        store_context: Arc::new(StoreContextService::new(
            store_trait.clone(),
            validator.clone(),
        )),
        monthly_rollup: Arc::new(MonthlyRollupService::new(store_trait, validator)),
    })
}

async fn shutdown_signal() {
    if let Err(err) = tokio::signal::ctrl_c().await {
        error!(error = %err, "Failed waiting for ctrl_c signal");
    }
}

async fn health_handler() -> Json<Value> {
    Json(json!({ "status": "ok", "transport": "http+grpc" }))
}

async fn calibrate_handler(
    State(state): State<Arc<AppState>>,
    Json(request): Json<CalibrateSessionHttpRequest>,
) -> ApiResult<CalibrationResultDto> {
    let trigger = request
        .trigger
        .as_deref()
        .filter(|v| !v.trim().is_empty())
        .unwrap_or("manual");

    let result = state
        .calibration
        .calibrate_async(
            &request.session_id,
            request.stability,
            request.friction,
            request.logic,
            request.autonomy,
            trigger,
        )
        .await
        .map_err(internal_error)?;

    Ok(Json(CalibrationResultDto {
        previous_avec: to_avec_dto(result.previous_avec),
        delta: result.delta,
        drift_classification: format!("{:?}", result.drift_classification),
        trigger: result.trigger,
        trigger_history: result.trigger_history,
        is_first_calibration: result.is_first_calibration,
    }))
}

async fn store_context_handler(
    State(state): State<Arc<AppState>>,
    Json(request): Json<StoreContextHttpRequest>,
) -> ApiResult<StoreResultDto> {
    let result = state
        .store_context
        .store_async(&request.node, &request.session_id)
        .await;

    Ok(Json(StoreResultDto {
        node_id: result.node_id,
        psi: result.psi,
        valid: result.valid,
        validation_error: result.validation_error,
    }))
}

async fn get_context_handler(
    State(state): State<Arc<AppState>>,
    Json(request): Json<GetContextHttpRequest>,
) -> ApiResult<RetrieveResultDto> {
    let limit = request.limit.unwrap_or(5);
    let result = state
        .context_query
        .get_context_async(
            &request.session_id,
            request.stability,
            request.friction,
            request.logic,
            request.autonomy,
            limit,
        )
        .await;

    Ok(Json(RetrieveResultDto {
        nodes: result.nodes.iter().map(to_node_dto).collect(),
        retrieved: result.retrieved,
        psi_range: PsiRangeDto {
            min: result.psi_range.min,
            max: result.psi_range.max,
            average: result.psi_range.average,
        },
    }))
}

async fn list_nodes_handler(
    State(state): State<Arc<AppState>>,
    Query(query): Query<ListNodesQuery>,
) -> ApiResult<ListNodesResultDto> {
    let limit = query.limit.unwrap_or(50);
    let result = state
        .context_query
        .list_nodes_async(limit, query.session_id.as_deref())
        .await
        .map_err(internal_error)?;

    Ok(Json(ListNodesResultDto {
        nodes: result.nodes.iter().map(to_node_dto).collect(),
        retrieved: result.retrieved,
    }))
}

async fn graph_handler(
    State(state): State<Arc<AppState>>,
    Query(query): Query<GraphQuery>,
) -> ApiResult<GraphResponse> {
    let capped_limit = query.limit.unwrap_or(1000).clamp(1, 5000);

    let result = state
        .context_query
        .list_nodes_async(capped_limit, query.session_id.as_deref())
        .await
        .map_err(internal_error)?;

    let mut ordered_nodes = result.nodes;
    ordered_nodes.sort_by(|a, b| b.timestamp.cmp(&a.timestamp));

    #[derive(Clone)]
    struct SessionGroup {
        id: String,
        label: String,
        nodes: Vec<core_models::SttpNode>,
        node_count: usize,
        avg_psi: f32,
        last_modified: DateTime<Utc>,
        size: usize,
    }

    let mut grouped_map: BTreeMap<String, Vec<core_models::SttpNode>> = BTreeMap::new();
    for node in &ordered_nodes {
        grouped_map
            .entry(node.session_id.clone())
            .or_default()
            .push(node.clone());
    }

    let mut grouped = grouped_map
        .into_iter()
        .map(|(id, mut nodes)| {
            nodes.sort_by(|a, b| b.timestamp.cmp(&a.timestamp));
            let node_count = nodes.len();
            let avg_psi = if node_count == 0 {
                0.0
            } else {
                nodes.iter().map(|n| n.psi).sum::<f32>() / node_count as f32
            };
            let last_modified = nodes
                .first()
                .map(|n| n.timestamp)
                .unwrap_or_else(Utc::now);
            let size = 16 + std::cmp::min(28, node_count * 2);

            SessionGroup {
                label: id.clone(),
                id,
                nodes,
                node_count,
                avg_psi,
                last_modified,
                size,
            }
        })
        .collect::<Vec<_>>();

    grouped.sort_by(|a, b| b.last_modified.cmp(&a.last_modified));

    let node_by_id = ordered_nodes
        .iter()
        .map(|node| (graph_node_id(node), node.clone()))
        .collect::<HashMap<_, _>>();

    let sessions = grouped
        .iter()
        .map(|session| {
            json!({
                "id": format!("s:{}", session.id),
                "label": session.label,
                "nodeCount": session.node_count,
                "avgPsi": session.avg_psi,
                "lastModified": session.last_modified.to_rfc3339(),
                "size": session.size
            })
        })
        .collect::<Vec<_>>();

    let nodes = ordered_nodes
        .iter()
        .map(|node| {
            json!({
                "id": graph_node_id(node),
                "sessionId": node.session_id,
                "label": format!("{} {}", node.tier, node.timestamp.format("%m-%d %H:%M")),
                "tier": node.tier,
                "timestamp": node.timestamp.to_rfc3339(),
                "psi": node.psi,
                "parentNodeId": node.parent_node_id,
                "size": 9
            })
        })
        .collect::<Vec<_>>();

    let mut edges = Vec::new();

    for i in 0..grouped.len().saturating_sub(1) {
        edges.push(json!({
            "id": format!("t-{i}"),
            "source": format!("s:{}", grouped[i].id),
            "target": format!("s:{}", grouped[i + 1].id),
            "kind": "timeline"
        }));
    }

    for i in 0..grouped.len() {
        let from = &grouped[i];
        let mut nearest: Option<usize> = None;
        let mut nearest_distance = f32::MAX;

        for (j, other) in grouped.iter().enumerate() {
            if i == j {
                continue;
            }
            let distance = (from.avg_psi - other.avg_psi).abs();
            if distance < nearest_distance {
                nearest_distance = distance;
                nearest = Some(j);
            }
        }

        if let Some(nearest_index) = nearest {
            if i < nearest_index {
                edges.push(json!({
                    "id": format!("s-{i}-{nearest_index}"),
                    "source": format!("s:{}", from.id),
                    "target": format!("s:{}", grouped[nearest_index].id),
                    "kind": "similarity"
                }));
            }
        }
    }

    for session in &grouped {
        for i in 0..session.nodes.len() {
            let current = &session.nodes[i];
            let current_id = graph_node_id(current);

            edges.push(json!({
                "id": format!("m-{}-{i}", session.id),
                "source": format!("s:{}", session.id),
                "target": current_id,
                "kind": "membership"
            }));

            if i + 1 < session.nodes.len() {
                let older = &session.nodes[i + 1];
                edges.push(json!({
                    "id": format!("nt-{}-{i}", session.id),
                    "source": current_id,
                    "target": graph_node_id(older),
                    "kind": "node_timeline"
                }));
            }

            if let Some(parent) = current.parent_node_id.as_ref() {
                if node_by_id.contains_key(parent) {
                    edges.push(json!({
                        "id": format!("l-{}-{i}", session.id),
                        "source": current_id,
                        "target": parent,
                        "kind": "lineage"
                    }));
                }
            }
        }
    }

    Ok(Json(GraphResponse {
        sessions,
        nodes,
        edges,
        retrieved: ordered_nodes.len(),
    }))
}

async fn get_moods_handler(
    State(state): State<Arc<AppState>>,
    Query(query): Query<GetMoodsQuery>,
) -> ApiResult<MoodCatalogResultDto> {
    let result = state.mood_catalog.get(
        query.target_mood.as_deref(),
        query.blend.unwrap_or(1.0),
        query.current_stability,
        query.current_friction,
        query.current_logic,
        query.current_autonomy,
    );

    Ok(Json(to_mood_catalog_dto(result)))
}

async fn create_monthly_rollup_handler(
    State(state): State<Arc<AppState>>,
    Json(request): Json<CreateMonthlyRollupHttpRequest>,
) -> ApiResult<MonthlyRollupResultDto> {
    let rollup_request = MonthlyRollupRequest {
        session_id: request.session_id,
        start_utc: request.start_date_utc,
        end_utc: request.end_date_utc,
        source_session_id: request.source_session_id,
        parent_node_id: request.parent_node_id,
        persist: request.persist.unwrap_or(true),
        limit: request.limit.unwrap_or(5000),
    };

    let result = state.monthly_rollup.create_async(rollup_request).await;
    Ok(Json(to_monthly_rollup_dto(result)))
}

fn internal_error(error: impl std::fmt::Display) -> (StatusCode, Json<ErrorResponse>) {
    (
        StatusCode::INTERNAL_SERVER_ERROR,
        Json(ErrorResponse {
            error: error.to_string(),
        }),
    )
}

fn graph_node_id(node: &core_models::SttpNode) -> String {
    format!(
        "n:{}|{}|{}|{:.4}",
        node.session_id,
        node.timestamp.to_rfc3339(),
        node.compression_depth,
        node.psi
    )
}

fn to_avec_dto(value: core_models::AvecState) -> AvecStateDto {
    AvecStateDto {
        stability: value.stability,
        friction: value.friction,
        logic: value.logic,
        autonomy: value.autonomy,
        psi: value.psi(),
    }
}

fn to_node_dto(value: &core_models::SttpNode) -> SttpNodeDto {
    SttpNodeDto {
        raw: value.raw.clone(),
        session_id: value.session_id.clone(),
        tier: value.tier.clone(),
        timestamp: value.timestamp,
        compression_depth: value.compression_depth,
        parent_node_id: value.parent_node_id.clone(),
        user_avec: to_avec_dto(value.user_avec),
        model_avec: to_avec_dto(value.model_avec),
        compression_avec: value.compression_avec.map(to_avec_dto),
        rho: value.rho,
        kappa: value.kappa,
        psi: value.psi,
    }
}

fn to_mood_catalog_dto(result: core_models::MoodCatalogResult) -> MoodCatalogResultDto {
    MoodCatalogResultDto {
        presets: result
            .presets
            .into_iter()
            .map(|preset| MoodPresetDto {
                name: preset.name,
                description: preset.description,
                avec: to_avec_dto(preset.avec),
            })
            .collect(),
        apply_guide: result.apply_guide,
        swap_preview: result.swap_preview.map(|preview| MoodSwapPreviewDto {
            target_mood: preview.target_mood,
            blend: preview.blend,
            current: to_avec_dto(preview.current),
            target: to_avec_dto(preview.target),
            blended: to_avec_dto(preview.blended),
        }),
    }
}

fn to_monthly_rollup_dto(result: core_models::MonthlyRollupResult) -> MonthlyRollupResultDto {
    MonthlyRollupResultDto {
        success: result.success,
        node_id: result.node_id,
        raw_node: result.raw_node,
        error: result.error,
        source_nodes: result.source_nodes,
        parent_reference: result.parent_reference,
        user_average: to_avec_dto(result.user_average),
        model_average: to_avec_dto(result.model_average),
        compression_average: to_avec_dto(result.compression_average),
        rho_range: to_numeric_range_dto(result.rho_range),
        kappa_range: to_numeric_range_dto(result.kappa_range),
        psi_range: to_numeric_range_dto(result.psi_range),
        rho_bands: to_confidence_bands_dto(result.rho_bands),
        kappa_bands: to_confidence_bands_dto(result.kappa_bands),
    }
}

fn to_numeric_range_dto(value: NumericRange) -> NumericRangeDto {
    NumericRangeDto {
        min: value.min,
        max: value.max,
        average: value.average,
    }
}

fn to_confidence_bands_dto(value: ConfidenceBandSummary) -> ConfidenceBandSummaryDto {
    ConfidenceBandSummaryDto {
        low: value.low,
        medium: value.medium,
        high: value.high,
    }
}

#[derive(Clone)]
struct GrpcGatewayService {
    state: Arc<AppState>,
}

impl GrpcGatewayService {
    fn new(state: Arc<AppState>) -> Self {
        Self { state }
    }
}

#[tonic::async_trait]
impl proto::sttp_gateway_service_server::SttpGatewayService for GrpcGatewayService {
    async fn calibrate_session(
        &self,
        request: Request<proto::CalibrateSessionRequest>,
    ) -> Result<Response<proto::CalibrateSessionReply>, Status> {
        let request = request.into_inner();
        let trigger = if request.trigger.trim().is_empty() {
            "manual"
        } else {
            &request.trigger
        };

        let result = self
            .state
            .calibration
            .calibrate_async(
                &request.session_id,
                request.stability,
                request.friction,
                request.logic,
                request.autonomy,
                trigger,
            )
            .await
            .map_err(|err| Status::internal(err.to_string()))?;

        let reply = proto::CalibrateSessionReply {
            previous_avec: Some(to_grpc_avec(result.previous_avec)),
            delta: result.delta,
            drift_classification: format!("{:?}", result.drift_classification),
            trigger: result.trigger,
            trigger_history: result.trigger_history,
            is_first_calibration: result.is_first_calibration,
        };

        Ok(Response::new(reply))
    }

    async fn store_context(
        &self,
        request: Request<proto::StoreContextRequest>,
    ) -> Result<Response<proto::StoreContextReply>, Status> {
        let request = request.into_inner();
        let result = self
            .state
            .store_context
            .store_async(&request.node, &request.session_id)
            .await;

        let reply = proto::StoreContextReply {
            node_id: result.node_id,
            psi: result.psi,
            valid: result.valid,
            validation_error: result.validation_error,
        };

        Ok(Response::new(reply))
    }

    async fn get_context(
        &self,
        request: Request<proto::GetContextRequest>,
    ) -> Result<Response<proto::GetContextReply>, Status> {
        let request = request.into_inner();

        let result = self
            .state
            .context_query
            .get_context_async(
                &request.session_id,
                request.stability,
                request.friction,
                request.logic,
                request.autonomy,
                if request.limit <= 0 {
                    5
                } else {
                    request.limit as usize
                },
            )
            .await;

        let reply = proto::GetContextReply {
            nodes: result.nodes.iter().map(to_grpc_node).collect(),
            retrieved: clamp_usize_to_i32(result.retrieved),
            psi_range: Some(to_grpc_psi_range(result.psi_range)),
        };

        Ok(Response::new(reply))
    }

    async fn list_nodes(
        &self,
        request: Request<proto::ListNodesRequest>,
    ) -> Result<Response<proto::ListNodesReply>, Status> {
        let request = request.into_inner();
        let result = self
            .state
            .context_query
            .list_nodes_async(
                if request.limit <= 0 {
                    50
                } else {
                    request.limit as usize
                },
                request.session_id.as_deref(),
            )
            .await
            .map_err(|err| Status::internal(err.to_string()))?;

        let reply = proto::ListNodesReply {
            nodes: result.nodes.iter().map(to_grpc_node).collect(),
            retrieved: clamp_usize_to_i32(result.retrieved),
        };

        Ok(Response::new(reply))
    }

    async fn get_moods(
        &self,
        request: Request<proto::GetMoodsRequest>,
    ) -> Result<Response<proto::GetMoodsReply>, Status> {
        let request = request.into_inner();
        let result = self.state.mood_catalog.get(
            request.target_mood.as_deref(),
            request.blend,
            request.current_stability,
            request.current_friction,
            request.current_logic,
            request.current_autonomy,
        );

        let reply = proto::GetMoodsReply {
            presets: result
                .presets
                .into_iter()
                .map(|preset| proto::MoodPreset {
                    name: preset.name,
                    description: preset.description,
                    avec: Some(to_grpc_avec(preset.avec)),
                })
                .collect(),
            apply_guide: result.apply_guide,
            swap_preview: result.swap_preview.map(|preview| proto::MoodSwapPreview {
                target_mood: preview.target_mood,
                blend: preview.blend,
                current: Some(to_grpc_avec(preview.current)),
                target: Some(to_grpc_avec(preview.target)),
                blended: Some(to_grpc_avec(preview.blended)),
            }),
        };

        Ok(Response::new(reply))
    }

    async fn create_monthly_rollup(
        &self,
        request: Request<proto::CreateMonthlyRollupRequest>,
    ) -> Result<Response<proto::CreateMonthlyRollupReply>, Status> {
        let request = request.into_inner();

        let start_utc = timestamp_from_proto(request.start_utc)?;
        let end_utc = timestamp_from_proto(request.end_utc)?;

        let monthly_request = MonthlyRollupRequest {
            session_id: request.session_id,
            start_utc,
            end_utc,
            source_session_id: request.source_session_id,
            parent_node_id: request.parent_node_id,
            persist: request.persist,
            limit: if request.limit <= 0 {
                5000
            } else {
                request.limit as usize
            },
        };

        let result = self.state.monthly_rollup.create_async(monthly_request).await;

        let reply = proto::CreateMonthlyRollupReply {
            success: result.success,
            node_id: result.node_id,
            raw_node: result.raw_node,
            error: result.error,
            source_nodes: clamp_usize_to_i32(result.source_nodes),
            parent_reference: result.parent_reference,
            user_average: Some(to_grpc_avec(result.user_average)),
            model_average: Some(to_grpc_avec(result.model_average)),
            compression_average: Some(to_grpc_avec(result.compression_average)),
            rho_range: Some(to_grpc_numeric_range(result.rho_range)),
            kappa_range: Some(to_grpc_numeric_range(result.kappa_range)),
            psi_range: Some(to_grpc_numeric_range(result.psi_range)),
            rho_bands: Some(to_grpc_confidence_bands(result.rho_bands)),
            kappa_bands: Some(to_grpc_confidence_bands(result.kappa_bands)),
        };

        Ok(Response::new(reply))
    }
}

fn to_grpc_avec(value: core_models::AvecState) -> proto::AvecState {
    proto::AvecState {
        stability: value.stability,
        friction: value.friction,
        logic: value.logic,
        autonomy: value.autonomy,
        psi: value.psi(),
    }
}

fn to_grpc_node(value: &core_models::SttpNode) -> proto::SttpNode {
    proto::SttpNode {
        raw: value.raw.clone(),
        session_id: value.session_id.clone(),
        tier: value.tier.clone(),
        timestamp: Some(timestamp_to_proto(value.timestamp)),
        compression_depth: value.compression_depth,
        parent_node_id: value.parent_node_id.clone(),
        user_avec: Some(to_grpc_avec(value.user_avec)),
        model_avec: Some(to_grpc_avec(value.model_avec)),
        compression_avec: value.compression_avec.map(to_grpc_avec),
        rho: value.rho,
        kappa: value.kappa,
        psi: value.psi,
    }
}

fn to_grpc_psi_range(value: PsiRange) -> proto::PsiRange {
    proto::PsiRange {
        min: value.min,
        max: value.max,
        average: value.average,
    }
}

fn to_grpc_numeric_range(value: NumericRange) -> proto::NumericRange {
    proto::NumericRange {
        min: value.min,
        max: value.max,
        average: value.average,
    }
}

fn to_grpc_confidence_bands(value: ConfidenceBandSummary) -> proto::ConfidenceBandSummary {
    proto::ConfidenceBandSummary {
        low: clamp_usize_to_i32(value.low),
        medium: clamp_usize_to_i32(value.medium),
        high: clamp_usize_to_i32(value.high),
    }
}

fn timestamp_to_proto(value: DateTime<Utc>) -> prost_types::Timestamp {
    prost_types::Timestamp {
        seconds: value.timestamp(),
        nanos: value.timestamp_subsec_nanos() as i32,
    }
}

fn timestamp_from_proto(value: Option<prost_types::Timestamp>) -> Result<DateTime<Utc>, Status> {
    let value = value.ok_or_else(|| Status::invalid_argument("missing timestamp"))?;
    DateTime::<Utc>::from_timestamp(value.seconds, value.nanos as u32)
        .ok_or_else(|| Status::invalid_argument("invalid timestamp"))
}

fn clamp_usize_to_i32(value: usize) -> i32 {
    if value > i32::MAX as usize {
        i32::MAX
    } else {
        value as i32
    }
}
