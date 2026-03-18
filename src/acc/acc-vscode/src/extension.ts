import * as vscode from "vscode";
import * as path from "path";
import { AccServerDownloader } from "./downloader";
import { spawn, ChildProcess, SpawnOptions } from "child_process";
import * as net from "net";

let accProcess: ChildProcess | undefined;
let accClient: AccClient | undefined;
let downloader: AccServerDownloader;
let outputChannel: vscode.OutputChannel;

export async function activate(context: vscode.ExtensionContext) {
  outputChannel = vscode.window.createOutputChannel("ACC");
  downloader = new AccServerDownloader(context, outputChannel);

  // Check if server is installed
  const serverPath = await downloader.ensureServerInstalled();

  if (!serverPath) {
    vscode.window.showWarningMessage(
      'ACC: Server not installed. Run "ACC: Download Server Binary" to install.',
    );

    // Register download command
    context.subscriptions.push(
      vscode.commands.registerCommand("acc.downloadServer", async () => {
        const path = await downloader.downloadServer();
        if (path) {
          // Restart activation after download
          await startAccEngine(path, context);
        }
      }),
    );
    return;
  }

  await startAccEngine(serverPath, context);

  // Give engine time to start
  setTimeout(() => {
    accClient = new AccClient("localhost", 9339);

    // Register LSP stream
    const language =
      vscode.window.activeTextEditor?.document.languageId || "csharp";
    accClient.registerLspStream(language, 9340);

    // Initial crawl on activation (optional)
    vscode.window.showInformationMessage(
      'ACC: Ready! Run "ACC: Build Dependency Graph" to index your codebase.',
    );
  }, 5000);

  // Hook into file saves to tap LSP data
  context.subscriptions.push(
    vscode.workspace.onDidSaveTextDocument(async (doc) => {
      await tapAndForwardSymbols(doc.uri);
    }),
  );

  // Hook into active editor changes (for real-time updates)
  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(async (editor) => {
      if (editor) {
        await tapAndForwardSymbols(editor.document.uri);
      }
    }),
  );

  // Register commands
  context.subscriptions.push(
    vscode.commands.registerCommand("acc.buildGraph", async () => {
      await buildDependencyGraph();
    }),
  );

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
      if (stats) {
        vscode.window.showInformationMessage(
          `ACC Stats: ${stats.totalNodes} nodes indexed`,
        );
      }
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("acc.showHighFriction", async () => {
      const nodes = await accClient?.getHighFriction(0.7, 20);
      showNodeList("High-Friction Nodes (Bottlenecks)", nodes);
    }),
  );

  context.subscriptions.push(
    vscode.commands.registerCommand("acc.showUnstable", async () => {
      const nodes = await accClient?.getUnstable(0.4, 20);
      showNodeList("Unstable Nodes (High Churn)", nodes);
    }),
  );

  console.log("ACC extension activated");
}

export function deactivate() {
  if (accProcess) {
    accProcess.kill();
  }
}

async function startAccEngine(
  serverPath: string,
  context: vscode.ExtensionContext,
) {
  const workspaceRoot = vscode.workspace.workspaceFolders?.[0]?.uri.fsPath;
  if (!workspaceRoot) {
    vscode.window.showWarningMessage("ACC: No workspace folder open");
    return;
  }

  const config = vscode.workspace.getConfiguration("acc");
  const useRemote = config.get<boolean>("database.remote", false);
  const useTelemetry = config.get<boolean>("telemetry.use", false);

  const args = [
    "--Acc:RepositoryPath",
    workspaceRoot,
    "--JsonRpc:Port",
    config.get<number>("rpcPort", 9339).toString(),
    "--SurrealDb:Remote",
    useRemote.toString(),
  ];

  if (useRemote) {
    args.push("--SurrealDb:Endpoints:Remote");
    args.push(
      config.get<string>("database.remoteEndpoint", "localhost:8000/rpc"),
    );
  }

  if (useTelemetry) {
    args.push("--Acc:Telemetry:Enabled");
    args.push("true");

    args.push("--Acc:Telemetry:Endpoint");
    args.push(config.get<string>("telemetry.endpoint", "localhost:4317"));
  }

  outputChannel.appendLine(`Starting ACC server: ${serverPath}`);
  outputChannel.appendLine(`Args: ${args.join(" ")}`);

  var opts  = {
    cwd: workspaceRoot || path.dirname(serverPath),
    detached: true,
      stdio: ['ignore', 'pipe', 'pipe'] as any, 
    env: {
      ...process.env,
      ELECTRON_RUN_AS_NODE: undefined,
      VSCODE_AMD_ENTRYPOINT: undefined,
      VSCODE_IPC_HOOK: undefined
    }
  };

  accProcess = spawn(serverPath, args, opts);
  accProcess.unref();

  // accProcess.stdout?.on("data", (data) => {
  //   outputChannel.appendLine(`[ACC] ${data}`);
  // });

  // accProcess.stderr?.on("data", (data) => {
  //   outputChannel.appendLine(`[ACC ERROR] ${data}`);
  // });

  accProcess.on("error", (err) => {
    vscode.window.showErrorMessage(`ACC failed to start: ${err.message}`);
    outputChannel.appendLine(`Failed to start: ${err}`);
  });

  accProcess.on("close", (code) => {
    outputChannel.appendLine(`ACC exited with code ${code}`);
  });

  // Give it time to start
  await new Promise((resolve) => setTimeout(resolve, 2000));

  // Initialize client
  accClient = new AccClient("localhost", config.get<number>("rpcPort", 9339));

  vscode.window.showInformationMessage("ACC server started successfully!");
}
async function tapAndForwardSymbols(uri: vscode.Uri) {
  try {
    // 1. Get document symbols from VSCode's LSP
    const symbols = await vscode.commands.executeCommand<
      vscode.DocumentSymbol[]
    >("vscode.executeDocumentSymbolProvider", uri);

    if (!symbols || symbols.length === 0) {
      console.log("No symbols found for", uri.fsPath);
      return;
    }

    console.log(`Tapped ${symbols.length} symbols from ${uri.fsPath}`);

    // 2. Convert to LSP message format
    const lspMessage = {
      jsonrpc: "2.0",
      id: Date.now(),
      method: "textDocument/documentSymbol",
      params: {
        textDocument: {
          uri: uri.toString(),
        },
      },
      result: symbols.map(convertSymbol),
    };

    // 3. Forward to ACC
    await forwardToAcc(lspMessage);

    // 4. Get references for each symbol to build edges
    for (const symbol of symbols) {
      await tapAndForwardReferences(uri, symbol);
    }
  } catch (err) {
    console.error("Error tapping symbols:", err);
  }
}

async function tapAndForwardReferences(
  uri: vscode.Uri,
  symbol: vscode.DocumentSymbol,
) {
  try {
    const refs = await vscode.commands.executeCommand<vscode.Location[]>(
      "vscode.executeReferenceProvider",
      uri,
      symbol.range.start,
    );

    if (!refs || refs.length === 0) return;

    console.log(`Found ${refs.length} references for ${symbol.name}`);

    // Forward reference data to ACC for edge building
    const edgeMessage = {
      jsonrpc: "2.0",
      id: Date.now(),
      method: "textDocument/references",
      params: {
        textDocument: { uri: uri.toString() },
        position: {
          line: symbol.range.start.line,
          character: symbol.range.start.character,
        },
        context: { includeDeclaration: false },
      },
      result: refs.map((loc) => ({
        uri: loc.uri.toString(),
        range: {
          start: {
            line: loc.range.start.line,
            character: loc.range.start.character,
          },
          end: { line: loc.range.end.line, character: loc.range.end.character },
        },
      })),
    };

    await forwardToAcc(edgeMessage);
  } catch (err) {
    console.error("Error tapping references:", err);
  }
}

function convertSymbol(symbol: vscode.DocumentSymbol): any {
  return {
    name: symbol.name,
    kind: symbol.kind,
    range: {
      start: {
        line: symbol.range.start.line,
        character: symbol.range.start.character,
      },
      end: {
        line: symbol.range.end.line,
        character: symbol.range.end.character,
      },
    },
    selectionRange: {
      start: {
        line: symbol.selectionRange.start.line,
        character: symbol.selectionRange.start.character,
      },
      end: {
        line: symbol.selectionRange.end.line,
        character: symbol.selectionRange.end.character,
      },
    },
    detail: symbol.detail,
    children: symbol.children?.map(convertSymbol),
  };
}

async function forwardToAcc(message: any) {
  return new Promise<void>((resolve, reject) => {
    const socket = net.connect(9340, "localhost");

    const content = JSON.stringify(message);
    const header = `Content-Length: ${content.length}\r\n\r\n`;
    const fullMessage = header + content;

    socket.on("connect", () => {
      socket.write(fullMessage);
      socket.end();
      resolve();
    });

    socket.on("error", (err) => {
      console.error("Error forwarding to ACC:", err);
      reject(err);
    });
  });
}

async function buildDependencyGraph() {
  await vscode.window.withProgress(
    {
      location: vscode.ProgressLocation.Notification,
      title: "Building dependency graph...",
      cancellable: false,
    },
    async (progress) => {
      // Find all relevant files
      const files = await vscode.workspace.findFiles(
        "**/*.{cs,ts,js,py,go,rs}",
        "**/node_modules/**",
      );

      let processed = 0;
      for (const file of files) {
        await tapAndForwardSymbols(file);

        processed++;
        progress.report({
          message: `${processed}/${files.length} files`,
          increment: (1 / files.length) * 100,
        });
      }

      vscode.window.showInformationMessage(
        `ACC: Indexed ${files.length} files!`,
      );
    },
  );
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
    vscode.window.showInformationMessage("No results found");
    return;
  }

  const items = results.map((node) => {
    // Format AVEC scores if available
    const avecInfo = node.avec
      ? `S:${node.avec.stability.toFixed(2)} L:${node.avec.logic.toFixed(2)} F:${node.avec.friction.toFixed(2)} A:${node.avec.autonomy.toFixed(2)}`
      : "No AVEC";

    return {
      label: node.name,
      description: `${node.type} @ L${node.line_start}`,
      detail: `${node.file_path} | ${avecInfo}`,
      node,
    };
  });

  vscode.window
    .showQuickPick(items, {
      placeHolder: "Select a node to navigate to",
    })
    .then((selected) => {
      if (selected) {
        const filePath = selected.node.file_path;
        // line_start from ACC is already 0-indexed (from LSP), no adjustment needed
        const line = selected.node.line_start;

        console.log("Opening file:", filePath, "at line:", line);

        const fileUri = vscode.Uri.file(filePath);

        vscode.workspace.openTextDocument(fileUri).then(
          (doc) => {
            vscode.window.showTextDocument(doc).then((editor) => {
              const position = new vscode.Position(line, 0);
              const range = new vscode.Range(position, position);

              editor.selection = new vscode.Selection(position, position);
              editor.revealRange(range, vscode.TextEditorRevealType.InCenter);

              console.log("Navigated to line:", line);
            });
          },
          (err) => {
            vscode.window.showErrorMessage(
              `Could not open file: ${filePath} - ${err.message}`,
            );
            console.error("Error opening file:", err);
          },
        );
      }
    });
}

function showNodeList(title: string, nodes: any[] | undefined) {
  if (!nodes || nodes.length === 0) {
    vscode.window.showInformationMessage(`${title}: No results`);
    return;
  }

  const items = nodes.map((node) => {
    const avecInfo = node.avec
      ? `S:${node.avec.stability.toFixed(2)} L:${node.avec.logic.toFixed(2)} F:${node.avec.friction.toFixed(2)} A:${node.avec.autonomy.toFixed(2)}`
      : "No AVEC";

    return {
      label: node.name,
      description: `${node.type} @ L${node.line_start}`,
      detail: `${avecInfo} | ${node.file_path}`,
      node,
    };
  });

  vscode.window
    .showQuickPick(items, {
      title,
      placeHolder: "Select a node to navigate to",
    })
    .then((selected) => {
      if (selected) {
        const filePath = selected.node.file_path;
        const line = selected.node.line_start;
        const fileUri = vscode.Uri.file(filePath);

        vscode.workspace.openTextDocument(fileUri).then(
          (doc) => {
            vscode.window.showTextDocument(doc).then((editor) => {
              const position = new vscode.Position(line, 0);
              const range = new vscode.Range(position, position);

              editor.selection = new vscode.Selection(position, position);
              editor.revealRange(range, vscode.TextEditorRevealType.InCenter);
            });
          },
          (err) => {
            vscode.window.showErrorMessage(`Could not open file: ${filePath}`);
          },
        );
      }
    });
}
