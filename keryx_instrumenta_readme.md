# KeryxInstrumenta

> *The open source tooling layer of the Keryx ecosystem.*

KeryxInstrumenta is a collection of standalone instruments built on protocols designed for persistent, stateful AI communication. Each tool is independent, production-ready, and interoperable with any model or architecture.

---

## Instruments

| Instrument | Status | Description |
|---|---|---|
| [sttp-mcp](./sttp-mcp) | `active` | MCP server for STTP context persistence, retrieval, and session calibration |

*More instruments coming as the ecosystem grows.*

---

## The Keryx Ecosystem

KeryxInstrumenta is the public entry point of a larger system:

```
KeryxFlux          Herald.   Orchestration layer.
KeryxMemento       Memory.   Persistence substrate.
KeryxCortex        Mind.     Multi-agent intelligence.
KeryxInstrumenta   Tools.    You are here.
```

The instruments in this repo are designed to work standalone. They are also the foundation on which the rest of the ecosystem is built.

---

## Philosophy

- **Protocol first.** Every instrument is built on an open, documented protocol. The implementation is replaceable. The contract is not.
- **Model agnostic.** No instrument assumes a specific model, provider, or architecture. If it speaks the protocol it works.
- **Infrastructure, not opinion.** These tools do not decide how you build. They give you the substrate to build on.

---

## Contributing

KeryxInstrumenta is open source. Contributions welcome — new instruments, adapters, compression handlers, storage backends.

Each instrument lives in its own directory with its own README, spec, and implementation.

---

*Part of the KeryxLabs ecosystem.*
*KeryxFlux → KeryxMemento → KeryxCortex*
*Herald. Memory. Mind.*
