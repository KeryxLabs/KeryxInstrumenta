```
src/
└── sttp-mcp/
    ├── Domain/
    │   ├── Models/
    │   │   ├── SttpNode.cs          ← parsed ⏣ node
    │   │   ├── AvecState.cs         ← attractor vector
    │   │   ├── CalibrationResult.cs ← response from calibrate_session
    │   │   ├── StoreResult.cs       ← response from store_context
    │   │   └── RetrieveResult.cs    ← response from get_context
    │   └── Contracts/
    │       ├── INodeStore.cs        ← read/write nodes
    │       └── INodeValidator.cs    ← tree-sitter validation
    ├── Application/
    │   ├── Tools/
    │   │   ├── CalibrateSession.cs
    │   │   ├── StoreContext.cs
    │   │   └── GetContext.cs
    │   └── Validation/
    │       └── TreeSitterValidator.cs
    ├── Storage/
    │   └── SurrealDbNodeStore.cs
    └── Host/
        └── Program.cs
```