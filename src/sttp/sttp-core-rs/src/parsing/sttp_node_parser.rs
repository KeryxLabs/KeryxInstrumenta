use std::collections::HashMap;

use chrono::{DateTime, Utc};
use once_cell::sync::Lazy;
use regex::{Captures, Regex};

use crate::domain::models::{AvecState, ParseResult, SttpNode};

static TIMESTAMP_RX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r#"timestamp:\s*"(?P<v>[^"]+)""#).expect("timestamp regex must compile")
});

static TIER_RX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"(?i)tier:\s*(?P<v>raw|daily|weekly|monthly|quarterly|yearly)")
        .expect("tier regex must compile")
});

static COMPRESSION_DEPTH_RX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"compression_depth:\s*(?P<v>\d+)").expect("compression_depth regex must compile")
});

static PARENT_NODE_RX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(
        r#"parent_node:\s*(?:ref:(?P<ref>[^,\s}\]]+)|"(?P<quoted>[^"]+)"|(?P<null>null))"#,
    )
    .expect("parent regex must compile")
});

static AVEC_RX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(
        r"(?s)(?P<name>user_avec|model_avec|compression_avec)\s*:\s*\{\s*stability\s*:\s*(?P<stability>[\d.]+),\s*friction\s*:\s*(?P<friction>[\d.]+),\s*logic\s*:\s*(?P<logic>[\d.]+),\s*autonomy\s*:\s*(?P<autonomy>[\d.]+)(?:,\s*psi\s*:\s*(?P<psi>[\d.]+))?\s*\}",
    )
    .expect("avec regex must compile")
});

static RHO_RX: Lazy<Regex> =
    Lazy::new(|| Regex::new(r"rho:\s*(?P<v>[\d.]+)").expect("rho regex must compile"));
static KAPPA_RX: Lazy<Regex> =
    Lazy::new(|| Regex::new(r"kappa:\s*(?P<v>[\d.]+)").expect("kappa regex must compile"));
static PSI_RX: Lazy<Regex> = Lazy::new(|| {
    Regex::new(r"(?s)⍉⟨.*?psi:\s*(?P<v>[\d.]+)").expect("psi regex must compile")
});

#[derive(Debug, Default, Clone, Copy)]
pub struct SttpNodeParser;

impl SttpNodeParser {
    pub fn new() -> Self {
        Self
    }

    pub fn try_parse(&self, raw: &str, session_id: &str) -> ParseResult {
        let metrics_block = extract_metrics_block(raw);

        let mut avec_map: HashMap<String, AvecState> = HashMap::new();
        for caps in AVEC_RX.captures_iter(raw) {
            if let Some(name) = caps.name("name") {
                avec_map.insert(name.as_str().to_string(), parse_avec(&caps));
            }
        }

        if let Some(caps) = AVEC_RX.captures(metrics_block) {
            if caps.name("name").map(|m| m.as_str()) == Some("compression_avec") {
                avec_map.insert("compression_avec".to_string(), parse_avec(&caps));
            }
        }

        let node = SttpNode {
            raw: raw.to_string(),
            session_id: session_id.to_string(),
            tier: TIER_RX
                .captures(raw)
                .and_then(|c| c.name("v"))
                .map(|m| m.as_str().to_string())
                .unwrap_or_default(),
            timestamp: parse_timestamp(raw),
            compression_depth: parse_int(&COMPRESSION_DEPTH_RX, raw),
            parent_node_id: parse_parent_node(raw),
            sync_key: String::new(),
            updated_at: Utc::now(),
            source_metadata: None,
            user_avec: avec_map
                .get("user_avec")
                .copied()
                .unwrap_or_else(AvecState::zero),
            model_avec: avec_map
                .get("model_avec")
                .copied()
                .unwrap_or_else(AvecState::zero),
            compression_avec: Some(
                avec_map
                    .get("compression_avec")
                    .copied()
                    .unwrap_or_else(AvecState::zero),
            ),
            rho: parse_float(&RHO_RX, metrics_block),
            kappa: parse_float(&KAPPA_RX, metrics_block),
            psi: parse_float(&PSI_RX, metrics_block),
        };

        ParseResult::ok(node)
    }
}

fn parse_avec(caps: &Captures<'_>) -> AvecState {
    AvecState {
        stability: parse_group_float(caps, "stability"),
        friction: parse_group_float(caps, "friction"),
        logic: parse_group_float(caps, "logic"),
        autonomy: parse_group_float(caps, "autonomy"),
    }
}

fn parse_timestamp(raw: &str) -> DateTime<Utc> {
    let maybe_ts = TIMESTAMP_RX
        .captures(raw)
        .and_then(|c| c.name("v"))
        .map(|m| m.as_str());

    if let Some(ts) = maybe_ts {
        if let Ok(parsed) = DateTime::parse_from_rfc3339(ts) {
            return parsed.with_timezone(&Utc);
        }
    }

    Utc::now()
}

fn parse_parent_node(raw: &str) -> Option<String> {
    let caps = PARENT_NODE_RX.captures(raw)?;
    if caps.name("null").is_some() {
        return None;
    }

    if let Some(v) = caps.name("ref") {
        return Some(v.as_str().to_string());
    }

    if let Some(v) = caps.name("quoted") {
        return Some(v.as_str().to_string());
    }

    None
}

fn parse_int(rx: &Regex, raw: &str) -> i32 {
    rx.captures(raw)
        .and_then(|c| c.name("v"))
        .and_then(|v| v.as_str().parse::<i32>().ok())
        .unwrap_or(0)
}

fn parse_float(rx: &Regex, raw: &str) -> f32 {
    rx.captures(raw)
        .and_then(|c| c.name("v"))
        .and_then(|v| v.as_str().parse::<f32>().ok())
        .unwrap_or(0.0)
}

fn parse_group_float(caps: &Captures<'_>, group: &str) -> f32 {
    caps.name(group)
        .and_then(|v| v.as_str().parse::<f32>().ok())
        .unwrap_or(0.0)
}

fn extract_metrics_block(raw: &str) -> &str {
    if let Some(idx) = raw.find("⍉⟨") {
        &raw[idx..]
    } else {
        ""
    }
}
