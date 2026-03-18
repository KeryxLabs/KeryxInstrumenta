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
exports.AccServerDownloader = void 0;
const vscode = __importStar(require("vscode"));
const https = __importStar(require("https"));
const http = __importStar(require("http"));
const fs = __importStar(require("fs"));
const path = __importStar(require("path"));
const tar = __importStar(require("tar"));
const ACC_VERSION = '0.2.0'; // Update this with releases
const GITHUB_RELEASES_URL = `https://github.com/KeryxLabs/KeryxInstrumenta/releases/download`;
class AccServerDownloader {
    constructor(context, outputChannel) {
        this.context = context;
        this.outputChannel = outputChannel;
    }
    getPlatformInfo() {
        const platform = process.platform;
        const arch = process.arch;
        let assetName;
        let binaryName;
        if (platform === 'darwin') {
            if (arch === 'arm64') {
                assetName = `acc-${ACC_VERSION}-macos-arm64.tar.gz`;
            }
            else {
                assetName = `acc-${ACC_VERSION}-macos-x64.tar.gz`;
            }
            binaryName = 'acc';
        }
        else if (platform === 'linux') {
            if (arch === 'arm64') {
                assetName = `acc-${ACC_VERSION}-linux-arm64.tar.gz`;
            }
            else {
                assetName = `acc-${ACC_VERSION}-linux-x64.tar.gz`;
            }
            binaryName = 'acc';
        }
        else if (platform === 'win32') {
            assetName = `acc-${ACC_VERSION}-win-x64.tar.gz`;
            binaryName = 'acc.exe';
        }
        else {
            return null;
        }
        return { platform, arch, assetName, binaryName };
    }
    getServerPath() {
        const config = vscode.workspace.getConfiguration('acc');
        const customPath = config.get('serverPath');
        if (customPath) {
            return customPath;
        }
        const platformInfo = this.getPlatformInfo();
        if (!platformInfo)
            return null;
        const serverDir = path.join(this.context.globalStorageUri.fsPath, 'server');
        const binaryPath = path.join(serverDir, platformInfo.binaryName);
        return fs.existsSync(binaryPath) ? binaryPath : null;
    }
    async ensureServerInstalled() {
        const existingPath = this.getServerPath();
        if (existingPath) {
            this.outputChannel.appendLine(`ACC server found at: ${existingPath}`);
            return existingPath;
        }
        this.outputChannel.appendLine('ACC server not found, downloading...');
        const result = await vscode.window.showInformationMessage('ACC server binary not found. Download now?', 'Download', 'Cancel');
        if (result !== 'Download') {
            return null;
        }
        return await this.downloadServer();
    }
    async downloadServer() {
        const platformInfo = this.getPlatformInfo();
        if (!platformInfo) {
            vscode.window.showErrorMessage(`ACC: Unsupported platform ${process.platform}-${process.arch}`);
            return null;
        }
        const downloadUrl = `${GITHUB_RELEASES_URL}/v${ACC_VERSION}/${platformInfo.assetName}`;
        const serverDir = path.join(this.context.globalStorageUri.fsPath, 'server');
        // Ensure directory exists
        if (!fs.existsSync(serverDir)) {
            fs.mkdirSync(serverDir, { recursive: true });
        }
        const archivePath = path.join(serverDir, platformInfo.assetName);
        const binaryPath = path.join(serverDir, platformInfo.binaryName);
        try {
            await vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: "Downloading ACC server...",
                cancellable: false
            }, async (progress) => {
                // Download
                progress.report({ message: 'Downloading binary...' });
                await this.downloadFile(downloadUrl, archivePath, progress);
                // Extract
                progress.report({ message: 'Extracting...' });
                if (platformInfo.assetName.endsWith('.tar.gz')) {
                    await tar.extract({
                        file: archivePath,
                        cwd: serverDir
                    });
                }
                else {
                    // Handle zip for Windows (you'll need a zip library like 'adm-zip')
                    const AdmZip = require('adm-zip');
                    const zip = new AdmZip(archivePath);
                    zip.extractAllTo(serverDir, true);
                }
                // Make executable on Unix
                if (process.platform !== 'win32') {
                    fs.chmodSync(binaryPath, 0o755);
                }
                // Cleanup archive
                fs.unlinkSync(archivePath);
            });
            this.outputChannel.appendLine(`ACC server installed at: ${binaryPath}`);
            vscode.window.showInformationMessage('ACC server downloaded successfully!');
            return binaryPath;
        }
        catch (err) {
            vscode.window.showErrorMessage(`Failed to download ACC server: ${err.message}`);
            this.outputChannel.appendLine(`Download error: ${err}`);
            return null;
        }
    }
    downloadFile(url, dest, progress) {
        return new Promise((resolve, reject) => {
            const file = fs.createWriteStream(dest);
            const protocol = url.startsWith('https') ? https : http;
            protocol.get(url, (response) => {
                if (response.statusCode === 302 || response.statusCode === 301) {
                    // Handle redirect
                    file.close();
                    fs.unlinkSync(dest);
                    return this.downloadFile(response.headers.location, dest, progress)
                        .then(resolve)
                        .catch(reject);
                }
                if (response.statusCode !== 200) {
                    reject(new Error(`Failed to download: ${response.statusCode}`));
                    return;
                }
                const contentType = (response.headers['content-type'] || '').toLowerCase();
                if (contentType.includes('text/html')) {
                    reject(new Error('Download did not return a binary asset (received HTML). Check release tag and asset name.'));
                    return;
                }
                const totalBytes = parseInt(response.headers['content-length'] || '0', 10);
                let downloadedBytes = 0;
                response.on('data', (chunk) => {
                    downloadedBytes += chunk.length;
                    if (totalBytes > 0) {
                        const percent = (downloadedBytes / totalBytes) * 100;
                        progress.report({
                            message: `${(downloadedBytes / 1024 / 1024).toFixed(1)} MB / ${(totalBytes / 1024 / 1024).toFixed(1)} MB`,
                            increment: percent
                        });
                    }
                });
                response.pipe(file);
                file.on('finish', () => {
                    file.close();
                    resolve();
                });
            }).on('error', (err) => {
                fs.unlink(dest, () => { });
                reject(err);
            });
            file.on('error', (err) => {
                fs.unlink(dest, () => { });
                reject(err);
            });
        });
    }
}
exports.AccServerDownloader = AccServerDownloader;
//# sourceMappingURL=downloader.js.map