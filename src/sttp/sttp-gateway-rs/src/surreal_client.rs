use anyhow::{Context, Result};
use serde_json::Value;
use sttp_core_rs::storage::surrealdb::QueryParams;
use sttp_core_rs::storage::{SurrealDbClient, SurrealDbRuntimeOptions};
use surrealdb::engine::any::{Any, connect};
use surrealdb::opt::auth::Root;

pub struct RuntimeSurrealDbClient {
    db: surrealdb::Surreal<Any>,
}

impl RuntimeSurrealDbClient {
    pub async fn connect(
        runtime: &SurrealDbRuntimeOptions,
        user: Option<&str>,
        password: Option<&str>,
    ) -> Result<Self> {
        let db = connect(runtime.endpoint.as_str())
            .await
            .with_context(|| {
                format!(
                    "failed to connect to SurrealDB endpoint '{}'",
                    runtime.endpoint
                )
            })?;

        if runtime.use_remote {
            let username = user.filter(|v| !v.trim().is_empty()).unwrap_or("root");
            let password = password.filter(|v| !v.trim().is_empty()).unwrap_or("root");

            db.signin(Root {
                username: username.to_string(),
                password: password.to_string(),
            })
                .await
                .context("failed to authenticate against remote SurrealDB")?;
        } else if let (Some(username), Some(password)) = (
            user.filter(|v| !v.trim().is_empty()),
            password.filter(|v| !v.trim().is_empty()),
        ) {
            let _ = db
                .signin(Root {
                    username: username.to_string(),
                    password: password.to_string(),
                })
                .await;
        }

        db.use_ns(runtime.namespace.as_str())
            .use_db(runtime.database.as_str())
            .await
            .with_context(|| {
                format!(
                    "failed to select namespace '{}' and database '{}'",
                    runtime.namespace, runtime.database
                )
            })?;

        Ok(Self { db })
    }

    fn is_read_query(query: &str) -> bool {
        query
            .trim_start()
            .to_ascii_uppercase()
            .starts_with("SELECT")
    }
}

#[tonic::async_trait]
impl SurrealDbClient for RuntimeSurrealDbClient {
    async fn raw_query(&self, query: &str, parameters: QueryParams) -> Result<Vec<Value>> {
        let response = if parameters.is_empty() {
            self.db.query(query).await?
        } else {
            self.db.query(query).bind(parameters).await?
        };

        let mut response = response.check()?;

        if !Self::is_read_query(query) {
            return Ok(Vec::new());
        }

        if let Ok(rows) = response.take::<Vec<Value>>(0) {
            return Ok(rows);
        }

        if let Ok(Some(row)) = response.take::<Option<Value>>(0) {
            return Ok(vec![row]);
        }

        Ok(Vec::new())
    }
}
