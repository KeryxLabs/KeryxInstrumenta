ACC Output Schema - High Level

1. Essential structural elements
- File list: All relevant files in the project, grouped by type (source, config, test, etc.)
- Modules/packages: Logical groupings of code, including dependencies and boundaries
- Classes/objects: Main entities, their properties, and methods
- Functions/procedures: Key operations, entry points, and their signatures
- Dependency graph: How files/modules/classes/functions relate and depend on each other

2. Key semantic features
- Relationships: How modules, classes, and functions interact (calls, inheritance, composition)
- Purpose: Brief description or intent of each major element (e.g., “handles authentication”)
- Complexity: Quantitative or qualitative measure (e.g., cyclomatic complexity, lines of code, nesting)
- Entry points: Main functions or classes where execution begins (e.g., main(), controllers)
- Data flow: How information moves through the system (inputs, outputs, transformations)
- Patterns: Notable architectural or design patterns (e.g., MVC, observer, singleton)

3. Quantitative context metrics (AVEC values)
- Stability: How consistent and reliable the codebase is (e.g., test coverage, error rates, code churn)
- Friction: Degree of resistance or complexity in making changes (e.g., coupling, technical debt, build times)
- Logic: Depth and clarity of reasoning in the code (e.g., documentation quality, logical structure, explicitness)
- Autonomy: How independently components operate (e.g., modularity, isolation, clear boundaries)
- Psi: Composite score summarizing overall context health and readiness

4. Customizable metadata (user preferences, project goals, context views)
- User/team preferences: Custom weighting, filters, or focus areas for context extraction
- Project goals: Stated objectives, priorities, or constraints (e.g., performance, maintainability, security)
- Context views: Multiple perspectives (developer, agent, reviewer) with tailored information
- Feedback/tuning: Mechanisms for users to adjust or refine context output
- History/audit: Record of context changes, extraction runs, and user interactions
