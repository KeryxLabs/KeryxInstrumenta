```
src/
└── sttp/
    ├── sttp-core/
    │   ├── Domain/                  ← reusable STTP models and contracts
    │   ├── Parsing/                 ← node parser
    │   ├── Application/Services/    ← core STTP use-cases
    │   ├── Application/Validation/  ← structural validation
    │   └── Storage/                 ← in-memory and SurrealDB storage adapters
    └── sttp-mcp/
        ├── Application/Tools/       ← MCP wrapper surface only
        └── Program.cs               ← MCP composition root
```