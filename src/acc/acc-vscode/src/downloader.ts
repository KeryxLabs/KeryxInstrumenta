import * as vscode from 'vscode';
import * as https from 'https';
import * as http from 'http';
import * as fs from 'fs';
import * as path from 'path';
import * as tar from 'tar';
import * as child_process from 'child_process';

const ACC_VERSION = '0.3.1'; // Update this with releases
const GITHUB_RELEASES_URL = `https://github.com/KeryxLabs/KeryxInstrumenta/releases/download`;
const ACC_RELEASE_TAG = `acc-engine/v${ACC_VERSION}`;

interface PlatformInfo {
    platform: string;
    arch: string;
    assetName: string;
    binaryName: string;
}

export class AccServerDownloader {
    private context: vscode.ExtensionContext;
    private outputChannel: vscode.OutputChannel;

    constructor(context: vscode.ExtensionContext, outputChannel: vscode.OutputChannel) {
        this.context = context;
        this.outputChannel = outputChannel;
    }

    private getPlatformInfo(): PlatformInfo | null {
        const platform = process.platform;
        const arch = process.arch;

        let assetName: string;
        let binaryName: string;

        if (platform === 'darwin') {
            if (arch === 'arm64') {
                assetName = `acc-${ACC_VERSION}-macos-arm64.tar.gz`;
            } else {
                assetName = `acc-${ACC_VERSION}-macos-x64.tar.gz`;
            }
            binaryName = 'acc';
        } else if (platform === 'linux') {
            if (arch === 'arm64') {
                assetName = `acc-${ACC_VERSION}-linux-arm64.tar.gz`;
            } else {
                assetName = `acc-${ACC_VERSION}-linux-x64.tar.gz`;
            }
            binaryName = 'acc';
        } else if (platform === 'win32') {
            assetName = `acc-${ACC_VERSION}-win-x64.tar.gz`;
            binaryName = 'acc.exe';
        } else {
            return null;
        }

        return { platform, arch, assetName, binaryName };
    }

    public getServerPath(): string | null {
        const config = vscode.workspace.getConfiguration('acc');
        const customPath = config.get<string>('serverPath');
        
        if (customPath) {
            return customPath;
        }

        const platformInfo = this.getPlatformInfo();
        if (!platformInfo) return null;

        const serverDir = path.join(this.context.globalStorageUri.fsPath, 'server');
        const binaryPath = path.join(serverDir, platformInfo.binaryName);

        return fs.existsSync(binaryPath) ? binaryPath : null;
    }

    public getLizardPath(): string | null {
        const config = vscode.workspace.getConfiguration('acc');
        const customPath = config.get<string>('lizardPath');

        if (customPath) {
            return customPath;
        }

        // If lizard is available globally (in PATH), return its name so callers can invoke it.
        if (this.isCommandAvailable('lizard')) {
            return 'lizard';
        }
        return null;
    }

    public async ensureServerInstalled(): Promise<string | null> {
        const existingPath = this.getServerPath();
        if (existingPath) {
            this.outputChannel.appendLine(`ACC server found at: ${existingPath}`);
            return existingPath;
        }

        this.outputChannel.appendLine('ACC server not found, downloading...');

        const result = await vscode.window.showInformationMessage(
            'ACC server binary not found. Download now?',
            'Download',
            'Cancel'
        );

        if (result !== 'Download') {
            return null;
        }

        return await this.downloadServer();
    }

    public async ensureLizardInstalled(): Promise<string | null> {
        const existingPath = this.getLizardPath();
        if (existingPath) {
            this.outputChannel.appendLine(`Lizard found at: ${existingPath}`);
            return existingPath;
        }

        this.outputChannel.appendLine('Lizard not found.');
        const result = await vscode.window.showInformationMessage(
            'Lizard not found. Install via pip?',
            'Install via pip',
            'Cancel'
        );

        if (result === 'Install via pip') {
            const ok = await this.installLizardViaPip();
            if (ok && this.isCommandAvailable('lizard')) {
                return 'lizard';
            }
            vscode.window.showErrorMessage('Failed to install lizard via pip. Install manually and retry.');
            return null;
        }

        return null;
    }

    private installLizardViaPip(): Promise<boolean> {
        return new Promise((resolve) => {
            const pyCandidates = ['python3', 'python'];
            const python = pyCandidates.find(p => this.isCommandAvailable(p));
            if (!python) {
                vscode.window.showErrorMessage('Python not found in PATH. Install Python to use pip.');
                resolve(false);
                return;
            }

            vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: 'Installing lizard via pip...',
                cancellable: false
            }, (progress) => {
                return new Promise<void>((innerResolve) => {
                    const args = ['-m', 'pip', 'install', '--upgrade', '--user', 'lizard'];
                    const proc = child_process.spawn(python, args);

                    proc.stdout.on('data', (d) => this.outputChannel.appendLine(d.toString()));
                    proc.stderr.on('data', (d) => this.outputChannel.appendLine(d.toString()));

                    proc.on('close', (code) => {
                        innerResolve();
                        resolve(code === 0);
                    });
                });
            });
        });
    }

    public async downloadServer(): Promise<string | null> {
        const platformInfo = this.getPlatformInfo();
        if (!platformInfo) {
            vscode.window.showErrorMessage(`ACC: Unsupported platform ${process.platform}-${process.arch}`);
            return null;
        }

        const downloadUrl = `${GITHUB_RELEASES_URL}/${ACC_RELEASE_TAG}/${platformInfo.assetName}`;
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
                } else {
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

        } catch (err: any) {
            vscode.window.showErrorMessage(`Failed to download ACC server: ${err.message}`);
            this.outputChannel.appendLine(`Download error: ${err}`);
            return null;
        }
    }

    

    private downloadFile(url: string, dest: string, progress: vscode.Progress<{message?: string, increment?: number}>): Promise<void> {
        return new Promise((resolve, reject) => {
            const file = fs.createWriteStream(dest);
            const protocol = url.startsWith('https') ? https : http;

            protocol.get(url, (response) => {
                if (response.statusCode === 302 || response.statusCode === 301) {
                    // Handle redirect
                    file.close();
                    fs.unlinkSync(dest);
                    return this.downloadFile(response.headers.location!, dest, progress)
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
                fs.unlink(dest, () => {});
                reject(err);
            });

            file.on('error', (err) => {
                fs.unlink(dest, () => {});
                reject(err);
            });
        });
    }

    private isCommandAvailable(cmd: string): boolean {
        try {
            const which = process.platform === 'win32' ? 'where' : 'which';
            const res = child_process.spawnSync(which, [cmd], { encoding: 'utf8' });
            return res.status === 0 && !!res.stdout && res.stdout.trim().length > 0;
        } catch {
            return false;
        }
    }

    
}