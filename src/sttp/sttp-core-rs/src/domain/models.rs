use chrono::{DateTime, Utc};
use std::fmt;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum DriftClassification {
    Intentional,
    Uncontrolled,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum ValidationFailureReason {
    None,
    ParseFailure,
    CoherenceFailure,
    MissingLayer,
    InvalidTier,
    NestingDepth,
    SchemaViolation,
}

impl fmt::Display for ValidationFailureReason {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            ValidationFailureReason::None => write!(f, "None"),
            ValidationFailureReason::ParseFailure => write!(f, "ParseFailure"),
            ValidationFailureReason::CoherenceFailure => write!(f, "CoherenceFailure"),
            ValidationFailureReason::MissingLayer => write!(f, "MissingLayer"),
            ValidationFailureReason::InvalidTier => write!(f, "InvalidTier"),
            ValidationFailureReason::NestingDepth => write!(f, "NestingDepth"),
            ValidationFailureReason::SchemaViolation => write!(f, "SchemaViolation"),
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct AvecState {
    pub stability: f32,
    pub friction: f32,
    pub logic: f32,
    pub autonomy: f32,
}

impl AvecState {
    pub fn psi(self) -> f32 {
        self.stability + self.friction + self.logic + self.autonomy
    }

    pub fn drift_from(self, previous: Self) -> f32 {
        self.psi() - previous.psi()
    }

    pub fn classify_drift(self, previous: Self) -> DriftClassification {
        let delta = self.drift_from(previous).abs();
        if delta > 0.3 {
            DriftClassification::Uncontrolled
        } else {
            DriftClassification::Intentional
        }
    }

    pub const fn zero() -> Self {
        Self {
            stability: 0.0,
            friction: 0.0,
            logic: 0.0,
            autonomy: 0.0,
        }
    }

    pub const fn focused() -> Self {
        Self {
            stability: 0.95,
            friction: 0.10,
            logic: 0.95,
            autonomy: 0.90,
        }
    }

    pub const fn creative() -> Self {
        Self {
            stability: 0.80,
            friction: 0.15,
            logic: 0.70,
            autonomy: 0.95,
        }
    }

    pub const fn analytical() -> Self {
        Self {
            stability: 0.90,
            friction: 0.20,
            logic: 0.98,
            autonomy: 0.85,
        }
    }

    pub const fn exploratory() -> Self {
        Self {
            stability: 0.75,
            friction: 0.30,
            logic: 0.65,
            autonomy: 0.90,
        }
    }

    pub const fn collaborative() -> Self {
        Self {
            stability: 0.85,
            friction: 0.10,
            logic: 0.80,
            autonomy: 0.70,
        }
    }

    pub const fn defensive() -> Self {
        Self {
            stability: 0.90,
            friction: 0.40,
            logic: 0.90,
            autonomy: 0.60,
        }
    }

    pub const fn passive() -> Self {
        Self {
            stability: 0.98,
            friction: 0.05,
            logic: 0.60,
            autonomy: 0.40,
        }
    }
}

impl Default for AvecState {
    fn default() -> Self {
        Self::zero()
    }
}

#[derive(Debug, Clone)]
pub struct SttpNode {
    pub raw: String,
    pub session_id: String,
    pub tier: String,
    pub timestamp: DateTime<Utc>,
    pub compression_depth: i32,
    pub parent_node_id: Option<String>,
    pub user_avec: AvecState,
    pub model_avec: AvecState,
    pub compression_avec: Option<AvecState>,
    pub rho: f32,
    pub kappa: f32,
    pub psi: f32,
}

#[derive(Debug, Clone)]
pub struct CalibrationResult {
    pub previous_avec: AvecState,
    pub delta: f32,
    pub drift_classification: DriftClassification,
    pub trigger: String,
    pub trigger_history: Vec<String>,
    pub is_first_calibration: bool,
}

#[derive(Debug, Clone)]
pub struct NodeQuery {
    pub limit: usize,
    pub session_id: Option<String>,
    pub from_utc: Option<DateTime<Utc>>,
    pub to_utc: Option<DateTime<Utc>>,
}

impl Default for NodeQuery {
    fn default() -> Self {
        Self {
            limit: 500,
            session_id: None,
            from_utc: None,
            to_utc: None,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct NumericRange {
    pub min: f32,
    pub max: f32,
    pub average: f32,
}

impl Default for NumericRange {
    fn default() -> Self {
        Self {
            min: 0.0,
            max: 0.0,
            average: 0.0,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub struct PsiRange {
    pub min: f32,
    pub max: f32,
    pub average: f32,
}

impl Default for PsiRange {
    fn default() -> Self {
        Self {
            min: 0.0,
            max: 0.0,
            average: 0.0,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Default)]
pub struct ConfidenceBandSummary {
    pub low: usize,
    pub medium: usize,
    pub high: usize,
}

#[derive(Debug, Clone, Default)]
pub struct RetrieveResult {
    pub nodes: Vec<SttpNode>,
    pub retrieved: usize,
    pub psi_range: PsiRange,
}

#[derive(Debug, Clone, Default)]
pub struct ListNodesResult {
    pub nodes: Vec<SttpNode>,
    pub retrieved: usize,
}

#[derive(Debug, Clone, Default)]
pub struct StoreResult {
    pub node_id: String,
    pub psi: f32,
    pub valid: bool,
    pub validation_error: Option<String>,
}

#[derive(Debug, Clone)]
pub struct ParseResult {
    pub success: bool,
    pub node: Option<SttpNode>,
    pub error: Option<String>,
}

impl ParseResult {
    pub fn ok(node: SttpNode) -> Self {
        Self {
            success: true,
            node: Some(node),
            error: None,
        }
    }

    pub fn fail(error: impl Into<String>) -> Self {
        Self {
            success: false,
            node: None,
            error: Some(error.into()),
        }
    }
}

#[derive(Debug, Clone)]
pub struct ValidationResult {
    pub is_valid: bool,
    pub error: Option<String>,
    pub reason: ValidationFailureReason,
}

impl ValidationResult {
    pub fn ok() -> Self {
        Self {
            is_valid: true,
            error: None,
            reason: ValidationFailureReason::None,
        }
    }

    pub fn fail(error: impl Into<String>, reason: ValidationFailureReason) -> Self {
        Self {
            is_valid: false,
            error: Some(error.into()),
            reason,
        }
    }
}

#[derive(Debug, Clone)]
pub struct MoodCatalogResult {
    pub presets: Vec<MoodPreset>,
    pub apply_guide: String,
    pub swap_preview: Option<MoodSwapPreview>,
}

#[derive(Debug, Clone)]
pub struct MoodPreset {
    pub name: String,
    pub description: String,
    pub avec: AvecState,
}

#[derive(Debug, Clone)]
pub struct MoodSwapPreview {
    pub target_mood: String,
    pub blend: f32,
    pub current: AvecState,
    pub target: AvecState,
    pub blended: AvecState,
}

#[derive(Debug, Clone)]
pub struct MonthlyRollupRequest {
    pub session_id: String,
    pub start_utc: DateTime<Utc>,
    pub end_utc: DateTime<Utc>,
    pub source_session_id: Option<String>,
    pub parent_node_id: Option<String>,
    pub limit: usize,
    pub persist: bool,
}

impl MonthlyRollupRequest {
    pub fn new(session_id: impl Into<String>, start_utc: DateTime<Utc>, end_utc: DateTime<Utc>) -> Self {
        Self {
            session_id: session_id.into(),
            start_utc,
            end_utc,
            source_session_id: None,
            parent_node_id: None,
            limit: 5000,
            persist: true,
        }
    }
}

#[derive(Debug, Clone)]
pub struct MonthlyRollupResult {
    pub success: bool,
    pub node_id: String,
    pub raw_node: String,
    pub error: Option<String>,
    pub source_nodes: usize,
    pub parent_reference: Option<String>,
    pub user_average: AvecState,
    pub model_average: AvecState,
    pub compression_average: AvecState,
    pub rho_range: NumericRange,
    pub kappa_range: NumericRange,
    pub psi_range: NumericRange,
    pub rho_bands: ConfidenceBandSummary,
    pub kappa_bands: ConfidenceBandSummary,
}

impl Default for MonthlyRollupResult {
    fn default() -> Self {
        Self {
            success: false,
            node_id: String::new(),
            raw_node: String::new(),
            error: None,
            source_nodes: 0,
            parent_reference: None,
            user_average: AvecState::zero(),
            model_average: AvecState::zero(),
            compression_average: AvecState::zero(),
            rho_range: NumericRange::default(),
            kappa_range: NumericRange::default(),
            psi_range: NumericRange::default(),
            rho_bands: ConfidenceBandSummary::default(),
            kappa_bands: ConfidenceBandSummary::default(),
        }
    }
}
