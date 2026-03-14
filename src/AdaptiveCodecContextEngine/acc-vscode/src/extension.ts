import * as vscode from "vscode";
import * as net from "net";

let accClient: AccClient | undefined;
let lspForwarder: LspForwarder | undefined;

export function activate(context: vscode.ExtensionContext) {
  console.log("ACC extension activating...");

  const config = vscode.workspace.getConfiguration("acc");
  const host = config.get<string>("rpcHost", "localhost");
  const port = config.get<number>("rpcPort", 9339);

  // Initialize ACC client
  accClient = new AccClient(host, port);

  // Register LSP stream with ACC
  const language =
    vscode.window.activeTextEditor?.document.languageId || "typescript";
  accClient.registerLspStream(language, 9340);

  // Start LSP forwarder if enabled
  if (config.get<boolean>("enableLspForwarding", true)) {
    lspForwarder = new LspForwarder(9340);
    lspForwarder.start();
  }

  // Register commands
  context.subscriptions.push(
    vscode.commands.registerCommand("acc.search", async () => {
      const query = await vscode.window.showInputBox({
        prompt: "Search for nodes by name",
      });
      if (query) {
        const results = await accClient?.search(query);
        showSearchResults(results);
      }
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("acc.showStats", async () => {
      const stats = await accClient?.getStats();
      vscode.window.showInformationMessage(
        `ACC Stats: ${stats?.totalNodes} nodes | ` +
          `Avg Stability: ${stats?.averageStability.toFixed(2)} | ` +
          `Avg Friction: ${stats?.averageFriction.toFixed(2)}`,
      );
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("acc.showHighFriction", async () => {
      const nodes = await accClient?.getHighFriction(0.7, 20);
      showNodeList("High-Friction Nodes", nodes);
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("acc.showUnstable", async () => {
      const nodes = await accClient?.getUnstable(0.4, 20);
      showNodeList("Unstable Nodes", nodes);
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("acc.findDependencies", async () => {
      const editor = vscode.window.activeTextEditor;
      if (!editor) return;

      const position = editor.selection.active;
      const filePath = editor.document.uri.fsPath;

      // Find node at current position
      const results = await accClient?.search(filePath);
      // TODO: Filter by line number

      if (results && results.length > 0) {
        const deps = await accClient?.queryDependencies(
          results[0].nodeId,
          "Both",
          3,
        );
        showNodeList("Dependencies", deps);
      }
    }),
  );

  console.log("ACC extension activated");
}

export function deactivate() {
  lspForwarder?.stop();
}

class AccClient {
  private host: string;
  private port: number;
  private requestId = 1;

  constructor(host: string, port: number) {
    this.host = host;
    this.port = port;
  }

  private async rpcCall(method: string, params: any): Promise<any> {
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
        if (resolved) return;

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
          } else {
            resolve(response.result);
          }
        } catch (err) {
          // Keep accumulating
        }
      });

      socket.on("end", () => {
        if (resolved) return;

        // Try one last time to parse
        if (buffer.trim()) {
          try {
            const response = JSON.parse(buffer);
            resolved = true;
            if (response.error) {
              reject(new Error(response.error.message));
            } else {
              resolve(response.result);
            }
          } catch (err) {
            reject(new Error("Invalid JSON: " + buffer));
          }
        } else {
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

  async registerLspStream(language: string, port: number) {
    return this.rpcCall("acc.registerLspStream", {
      type: "tcp",
      language,
      port,
    });
  }

  async search(name: string, limit = 10) {
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

  async queryDependencies(nodeId: string, direction = "Both", maxDepth = -1) {
    return this.rpcCall("acc.queryDependencies", {
      nodeId,
      direction,
      maxDepth,
      includeScores: true,
    });
  }
}

class LspForwarder {
  private server: net.Server | undefined;
  private port: number;

  constructor(port: number) {
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
function showSearchResults(results: any[] | undefined) {
    if (!results || results.length === 0) {
        vscode.window.showInformationMessage('No results found');
        return;
    }

    const items = results.map(node => {
        // Format AVEC scores if available
        const avecInfo = node.avec 
            ? `S:${node.avec.stability.toFixed(2)} L:${node.avec.logic.toFixed(2)} F:${node.avec.friction.toFixed(2)} A:${node.avec.autonomy.toFixed(2)}`
            : 'No AVEC';
        
        return {
            label: node.name,
            description: `${node.type} @ L${node.line_start}`,
            detail: `${node.file_path} | ${avecInfo}`,
            node
        };
    });

    vscode.window.showQuickPick(items, {
        placeHolder: 'Select a node to navigate to'
    }).then(selected => {
        if (selected) {
            const filePath = selected.node.file_path;
            // line_start from ACC is already 0-indexed (from LSP), no adjustment needed
            const line = selected.node.line_start;
            
            console.log('Opening file:', filePath, 'at line:', line);
            
            const fileUri = vscode.Uri.file(filePath);
            
            vscode.workspace.openTextDocument(fileUri).then(
                doc => {
                    vscode.window.showTextDocument(doc).then(editor => {
                        const position = new vscode.Position(line, 0);
                        const range = new vscode.Range(position, position);
                        
                        editor.selection = new vscode.Selection(position, position);
                        editor.revealRange(range, vscode.TextEditorRevealType.InCenter);
                        
                        console.log('Navigated to line:', line);
                    });
                },
                err => {
                    vscode.window.showErrorMessage(`Could not open file: ${filePath} - ${err.message}`);
                    console.error('Error opening file:', err);
                }
            );
        }
    });
}

function showNodeList(title: string, nodes: any[] | undefined) {
    if (!nodes || nodes.length === 0) {
        vscode.window.showInformationMessage(`${title}: No results`);
        return;
    }

    const items = nodes.map(node => {
        const avecInfo = node.avec
            ? `S:${node.avec.stability.toFixed(2)} L:${node.avec.logic.toFixed(2)} F:${node.avec.friction.toFixed(2)} A:${node.avec.autonomy.toFixed(2)}`
            : 'No AVEC';
        
        return {
            label: node.name,
            description: `${node.type} @ L${node.line_start}`,
            detail: `${avecInfo} | ${node.file_path}`,
            node
        };
    });

    vscode.window.showQuickPick(items, { 
        title,
        placeHolder: 'Select a node to navigate to'
    }).then(selected => {
        if (selected) {
            const filePath = selected.node.file_path;
            const line = selected.node.line_start;
            const fileUri = vscode.Uri.file(filePath);
            
            vscode.workspace.openTextDocument(fileUri).then(
                doc => {
                    vscode.window.showTextDocument(doc).then(editor => {
                        const position = new vscode.Position(line, 0);
                        const range = new vscode.Range(position, position);
                        
                        editor.selection = new vscode.Selection(position, position);
                        editor.revealRange(range, vscode.TextEditorRevealType.InCenter);
                    });
                },
                err => {
                    vscode.window.showErrorMessage(`Could not open file: ${filePath}`);
                }
            );
        }
    });
}