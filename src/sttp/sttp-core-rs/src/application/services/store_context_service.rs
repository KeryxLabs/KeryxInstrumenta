use std::sync::Arc;

use crate::domain::contracts::{NodeStore, NodeValidator};
use crate::domain::models::StoreResult;
use crate::parsing::SttpNodeParser;

pub struct StoreContextService {
    store: Arc<dyn NodeStore>,
    validator: Arc<dyn NodeValidator>,
    parser: SttpNodeParser,
}

impl StoreContextService {
    pub fn new(store: Arc<dyn NodeStore>, validator: Arc<dyn NodeValidator>) -> Self {
        Self {
            store,
            validator,
            parser: SttpNodeParser::new(),
        }
    }

    pub async fn store_async(&self, node: &str, session_id: &str) -> StoreResult {
        let validation = self.validator.validate(node);
        if !validation.is_valid {
            return StoreResult {
                node_id: String::new(),
                psi: 0.0,
                valid: false,
                validation_error: Some(format!(
                    "{}: {}",
                    validation.reason,
                    validation.error.unwrap_or_default()
                )),
            };
        }

        let parse_result = self.parser.try_parse(node, session_id);
        if !parse_result.success {
            return StoreResult {
                node_id: String::new(),
                psi: 0.0,
                valid: false,
                validation_error: Some(format!(
                    "ParseFailure: {}",
                    parse_result.error.unwrap_or_default()
                )),
            };
        }

        let parsed = match parse_result.node {
            Some(node) => node,
            None => {
                return StoreResult {
                    node_id: String::new(),
                    psi: 0.0,
                    valid: false,
                    validation_error: Some("ParseFailure: missing parsed node".to_string()),
                }
            }
        };

        match self.store.store_async(parsed.clone()).await {
            Ok(node_id) => StoreResult {
                node_id,
                psi: parsed.psi,
                valid: true,
                validation_error: None,
            },
            Err(err) => StoreResult {
                node_id: String::new(),
                psi: 0.0,
                valid: false,
                validation_error: Some(format!("StoreFailure: {err}")),
            },
        }
    }
}
