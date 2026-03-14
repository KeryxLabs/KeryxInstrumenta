"use strict";
var __createBinding = (this && this.__createBinding) || (Object.create ? (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    var desc = Object.getOwnPropertyDescriptor(m, k);
    if (!desc || ("get" in desc ? !m.__esModule : desc.writable || desc.configurable)) {
      desc = { enumerable: true, get: function() { return m[k]; } };
    }
    Object.defineProperty(o, k2, desc);
}) : (function(o, m, k, k2) {
    if (k2 === undefined) k2 = k;
    o[k2] = m[k];
}));
var __setModuleDefault = (this && this.__setModuleDefault) || (Object.create ? (function(o, v) {
    Object.defineProperty(o, "default", { enumerable: true, value: v });
}) : function(o, v) {
    o["default"] = v;
});
var __importStar = (this && this.__importStar) || (function () {
    var ownKeys = function(o) {
        ownKeys = Object.getOwnPropertyNames || function (o) {
            var ar = [];
            for (var k in o) if (Object.prototype.hasOwnProperty.call(o, k)) ar[ar.length] = k;
            return ar;
        };
        return ownKeys(o);
    };
    return function (mod) {
        if (mod && mod.__esModule) return mod;
        var result = {};
        if (mod != null) for (var k = ownKeys(mod), i = 0; i < k.length; i++) if (k[i] !== "default") __createBinding(result, mod, k[i]);
        __setModuleDefault(result, mod);
        return result;
    };
})();
Object.defineProperty(exports, "__esModule", { value: true });
exports.activate = activate;
exports.deactivate = deactivate;
const vscode = __importStar(require("vscode"));
const net = __importStar(require("net"));
let accClient;
let lspForwarder;
function activate(context) {
    console.log("ACC extension activating...");
    const config = vscode.workspace.getConfiguration("acc");
    const host = config.get("rpcHost", "localhost");
    const port = config.get("rpcPort", 9339);
    // Initialize ACC client
    accClient = new AccClient(host, port);
    // Register LSP stream with ACC
    const language = vscode.window.activeTextEditor?.document.languageId || "typescript";
    accClient.registerLspStream(language, 9340);
    // Start LSP forwarder if enabled
    if (config.get("enableLspForwarding", true)) {
        lspForwarder = new LspForwarder(9340);
        lspForwarder.start();
    }
    // Register commands
    context.subscriptions.push(vscode.commands.registerCommand("acc.search", async () => {
        const query = await vscode.window.showInputBox({
            prompt: "Search for nodes by name",
        });
        if (query) {
            const results = await accClient?.search(query);
            showSearchResults(results);
        }
    }));
    context.subscriptions.push(vscode.commands.registerCommand("acc.showStats", async () => {
        const stats = await accClient?.getStats();
        vscode.window.showInformationMessage(`ACC Stats: ${stats?.totalNodes} nodes | ` +
            `Avg Stability: ${stats?.averageStability.toFixed(2)} | ` +
            `Avg Friction: ${stats?.averageFriction.toFixed(2)}`);
    }));
    context.subscriptions.push(vscode.commands.registerCommand("acc.showHighFriction", async () => {
        const nodes = await accClient?.getHighFriction(0.7, 20);
        showNodeList("High-Friction Nodes", nodes);
    }));
    context.subscriptions.push(vscode.commands.registerCommand("acc.showUnstable", async () => {
        const nodes = await accClient?.getUnstable(0.4, 20);
        showNodeList("Unstable Nodes", nodes);
    }));
    context.subscriptions.push(vscode.commands.registerCommand("acc.findDependencies", async () => {
        const editor = vscode.window.activeTextEditor;
        if (!editor)
            return;
        const position = editor.selection.active;
        const filePath = editor.document.uri.fsPath;
        // Find node at current position
        const results = await accClient?.search(filePath);
        // TODO: Filter by line number
        if (results && results.length > 0) {
            const deps = await accClient?.queryDependencies(results[0].nodeId, "Both", 3);
            showNodeList("Dependencies", deps);
        }
    }));
    console.log("ACC extension activated");
}
function deactivate() {
    lspForwarder?.stop();
}
class AccClient {
    constructor(host, port) {
        this.requestId = 1;
        this.host = host;
        this.port = port;
    }
    async rpcCall(method, params) {
        return new Promise((resolve, reject) => {
            const socket = net.connect(this.port, this.host);
            const request = {
                jsonrpc: "2.0",
                id: this.requestId++,
                method,
                params,
            };
            const message = JSON.stringify(request) + "\n";
            let buffer = "";
            let resolved = false;
            socket.on("connect", () => {
                console.log(`Connected to ACC`);
                socket.write(message);
            });
            socket.on("data", (data) => {
                if (resolved)
                    return;
                let chunk = data.toString("utf8");
                // Strip UTF-8 BOM if present
                if (chunk.charCodeAt(0) === 0xfeff) {
                    chunk = chunk.substring(1);
                }
                buffer += chunk;
                console.log("Buffer now:", buffer.length, "bytes");
                // Try to parse
                try {
                    const response = JSON.parse(buffer);
                    console.log("Successfully parsed response");
                    resolved = true;
                    socket.destroy();
                    if (response.error) {
                        reject(new Error(response.error.message));
                    }
                    else {
                        resolve(response.result);
                    }
                }
                catch (err) {
                    // Keep accumulating
                }
            });
            socket.on("end", () => {
                if (resolved)
                    return;
                // Try one last time to parse
                if (buffer.trim()) {
                    try {
                        const response = JSON.parse(buffer);
                        resolved = true;
                        if (response.error) {
                            reject(new Error(response.error.message));
                        }
                        else {
                            resolve(response.result);
                        }
                    }
                    catch (err) {
                        reject(new Error("Invalid JSON: " + buffer));
                    }
                }
                else {
                    reject(new Error("No response"));
                }
            });
            socket.on("error", (err) => {
                if (!resolved) {
                    reject(err);
                }
            });
            socket.on("timeout", () => {
                if (!resolved) {
                    socket.destroy();
                    reject(new Error("Timeout"));
                }
            });
            socket.setTimeout(5000);
        });
    }
    async registerLspStream(language, port) {
        return this.rpcCall("acc.registerLspStream", {
            type: "tcp",
            language,
            port,
        });
    }
    async search(name, limit = 10) {
        return this.rpcCall("acc.search", { name, limit });
    }
    async getStats() {
        return this.rpcCall("acc.getStats", {});
    }
    async getHighFriction(minFriction = 0.7, limit = 20) {
        return this.rpcCall("acc.getHighFriction", { minFriction, limit });
    }
    async getUnstable(maxStability = 0.4, limit = 20) {
        return this.rpcCall("acc.getUnstable", { maxStability, limit });
    }
    async queryDependencies(nodeId, direction = "Both", maxDepth = -1) {
        return this.rpcCall("acc.queryDependencies", {
            nodeId,
            direction,
            maxDepth,
            includeScores: true,
        });
    }
}
class LspForwarder {
    constructor(port) {
        this.port = port;
    }
    start() {
        // Hook into VSCode's language client messages
        // This is complex - for now, we'll skip auto-forwarding
        // and let users manually trigger queries
        console.log("LSP forwarding placeholder - will implement after MVP");
    }
    stop() {
        this.server?.close();
    }
}
function showSearchResults(results) {
    if (!results || results.length === 0) {
        vscode.window.showInformationMessage("No results found");
        return;
    }
    const items = results.map((node) => ({
        label: node.name,
        description: node.filePath,
        detail: `${node.type} | Stability: ${node.avec?.stability.toFixed(2)} | Friction: ${node.avec?.friction.toFixed(2)}`,
        node,
    }));
    vscode.window.showQuickPick(items).then((selected) => {
        if (selected) {
            // Open file at line
            vscode.workspace.openTextDocument(selected.node.filePath).then((doc) => {
                vscode.window.showTextDocument(doc).then((editor) => {
                    const position = new vscode.Position(selected.node.lineStart, 0);
                    editor.selection = new vscode.Selection(position, position);
                    editor.revealRange(new vscode.Range(position, position));
                });
            });
        }
    });
}
function showNodeList(title, nodes) {
    if (!nodes || nodes.length === 0) {
        vscode.window.showInformationMessage(`${title}: No results`);
        return;
    }
    const items = nodes.map((node) => ({
        label: node.name,
        description: node.filePath,
        detail: `Stability: ${node.avec?.stability.toFixed(2)} | Logic: ${node.avec?.logic.toFixed(2)} | Friction: ${node.avec?.friction.toFixed(2)}`,
        node,
    }));
    vscode.window.showQuickPick(items, { title });
}
//# sourceMappingURL=extension.js.map