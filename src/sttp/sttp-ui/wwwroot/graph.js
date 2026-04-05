let cytoscapeModulePromise;
const graphRegistry = new Map();

function getCytoscapeModule() {
    if (!cytoscapeModulePromise) {
        cytoscapeModulePromise = import("https://esm.sh/cytoscape@3.30.1");
    }
    return cytoscapeModulePromise;
}

function toElements(payload) {
    const nodes = (payload?.nodes ?? []).map(n => ({
        data: {
            id: n.id,
            nodeType: n.nodeType,
            sessionId: n.sessionId,
            label: n.label,
            nodeIndex: n.nodeIndex,
            nodeCount: n.nodeCount,
            avgPsi: n.avgPsi,
            psi: n.psi,
            size: n.size
        }
    }));

    const edges = (payload?.edges ?? []).map(e => ({
        data: {
            id: e.id,
            source: e.source,
            target: e.target,
            kind: e.kind
        }
    }));

    return nodes.concat(edges);
}

function getStyles(selectedSessionId) {
    return [
        {
            selector: "node[nodeType = 'session']",
            style: {
                label: "data(label)",
                "font-size": 10,
                "font-family": "'DM Mono', monospace",
                "text-wrap": "wrap",
                "text-max-width": 140,
                "text-valign": "bottom",
                "text-margin-y": 6,
                color: "#fdf7ec",
                "text-background-color": "rgba(61,42,30,0.54)",
                "text-background-opacity": 1,
                "text-background-padding": "3px",
                "text-background-shape": "roundrectangle",
                width: "mapData(size, 16, 44, 16, 44)",
                height: "mapData(size, 16, 44, 16, 44)",
                "background-color": "#c8522a",
                "background-opacity": 0.86,
                "border-color": "#fff8f2",
                "border-width": 2
            }
        },
        {
            selector: "node[nodeType = 'sttp']",
            style: {
                label: "data(label)",
                "font-size": 8,
                "font-family": "'DM Mono', monospace",
                "text-wrap": "wrap",
                "text-max-width": 120,
                "text-valign": "bottom",
                "text-margin-y": 4,
                color: "#fdf7ec",
                "text-background-color": "rgba(61,42,30,0.50)",
                "text-background-opacity": 1,
                "text-background-padding": "2.5px",
                "text-background-shape": "roundrectangle",
                width: "mapData(size, 8, 16, 8, 16)",
                height: "mapData(size, 8, 16, 8, 16)",
                "background-color": "#f2d7b6",
                "background-opacity": 0.93,
                "border-color": "#be9a74",
                "border-width": 1.5
            }
        },
        {
            selector: `node[id = \"${selectedSessionId}\"]`,
            style: {
                "background-color": "#235a44",
                "border-color": "#fdf7ec",
                "border-width": 3,
                "shadow-blur": 16,
                "shadow-color": "#235a44",
                "shadow-opacity": 0.3,
                "shadow-offset-x": 0,
                "shadow-offset-y": 0
            }
        },
        {
            selector: "edge[kind = 'timeline']",
            style: {
                width: 1.8,
                "line-color": "#6e5f54",
                "line-style": "solid",
                opacity: 0.52,
                "curve-style": "bezier"
            }
        },
        {
            selector: "edge[kind = 'similarity']",
            style: {
                width: 1.2,
                "line-color": "#2f6d49",
                "line-style": "dashed",
                opacity: 0.42,
                "curve-style": "bezier"
            }
        },
        {
            selector: "edge[kind = 'membership']",
            style: {
                width: 0.9,
                "line-color": "#b48c69",
                opacity: 0.35,
                "curve-style": "bezier"
            }
        },
        {
            selector: "edge[kind = 'node_timeline']",
            style: {
                width: 1.0,
                "line-color": "#8a775f",
                opacity: 0.44,
                "curve-style": "bezier"
            }
        },
        {
            selector: "edge[kind = 'lineage']",
            style: {
                width: 1.4,
                "line-color": "#7f4f3a",
                "line-style": "dotted",
                opacity: 0.55,
                "curve-style": "bezier"
            }
        },
        {
            selector: ":selected",
            style: {
                "overlay-color": "#c8522a",
                "overlay-opacity": 0.1,
                "overlay-padding": 8
            }
        }
    ];
}

export async function renderSessionGraph(elementId, payload, dotNetRef) {
    const container = document.getElementById(elementId);
    if (!container) return;

    const { default: cytoscape } = await getCytoscapeModule();

    const existing = graphRegistry.get(elementId);
    if (existing) {
        existing.destroy();
        graphRegistry.delete(elementId);
    }

    const cy = cytoscape({
        container,
        elements: toElements(payload),
        style: getStyles(payload?.selectedSessionId),
        layout: {
            name: "cose",
            fit: true,
            animate: true,
            animationDuration: 340,
            nodeRepulsion: 17000,
            idealEdgeLength: 96,
            gravity: 0.52
        },
        minZoom: 0.35,
        maxZoom: 2.7,
        wheelSensitivity: 0.16
    });

    cy.on("tap", "node", evt => {
        const nodeType = evt.target.data("nodeType");
        if (nodeType === "sttp") {
            const sessionId = evt.target.data("sessionId");
            const nodeIndex = evt.target.data("nodeIndex");
            if (typeof sessionId === "string" && Number.isInteger(nodeIndex)) {
                dotNetRef.invokeMethodAsync("OnGraphNodeSelected", sessionId, nodeIndex);
            }
            return;
        }

        const sessionId = evt.target.data("sessionId");
        if (!sessionId) return;
        dotNetRef.invokeMethodAsync("OnGraphSessionSelected", sessionId);
    });

    cy.on("mouseover", "node", evt => {
        evt.target.animate({
            style: { "shadow-opacity": 0.34, "shadow-blur": 18 },
            duration: 120
        });
    });

    cy.on("mouseout", "node", evt => {
        evt.target.animate({
            style: { "shadow-opacity": 0, "shadow-blur": 0 },
            duration: 120
        });
    });

    graphRegistry.set(elementId, cy);
}

export function destroySessionGraph(elementId) {
    const existing = graphRegistry.get(elementId);
    if (!existing) return;
    existing.destroy();
    graphRegistry.delete(elementId);
}
