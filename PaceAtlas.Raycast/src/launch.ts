import { getPreferenceValues } from "@raycast/api";
import { spawn, execFileSync } from "node:child_process";
import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import os from "node:os";
import net from "node:net";

const PIPE = "\\\\.\\pipe\\PaceAtlas.Api.v1";
type Preferences = { applicationPath?: string };
let pendingStart: Promise<void> | undefined;

function executableFrom(input: string): string | undefined {
  const candidate = input.trim().replace(/^"|"$/g, "");
  if (!candidate) return undefined;
  if (existsSync(candidate)) return candidate;
  const nested = path.join(candidate, "PaceAtlas.WinUI.exe");
  return existsSync(nested) ? nested : undefined;
}
// Raycast can run in a packaged process whose environment differs from an
// interactive PowerShell session. Probe the known per-user locations instead
// of assuming LOCALAPPDATA is always populated.
function localAppDataCandidates(): string[] {
  const candidates = [
    process.env.LOCALAPPDATA,
    process.env.USERPROFILE && path.join(process.env.USERPROFILE, "AppData", "Local"),
    process.env.APPDATA && path.resolve(process.env.APPDATA, "..", "Local"),
    path.join(os.homedir(), "AppData", "Local"),
  ];
  return [...new Set(candidates.filter((item): item is string => Boolean(item?.trim())).map(item => path.normalize(item)))];
}

function installerPath(attempts: string[]): string | undefined {
  for (const base of localAppDataCandidates()) {
    const config = path.join(base, "PaceAtlas", "installation.json");
    if (!existsSync(config)) { attempts.push(`Konfiguration fehlt: ${config}`); continue; }
    try {
      // Accept UTF-8 BOM, which may be written by Windows PowerShell.
      const data: unknown = JSON.parse(readFileSync(config, "utf8").replace(/^\uFEFF/, ""));
      if (!data || typeof data !== "object" || !("executablePath" in data) || typeof data.executablePath !== "string") {
        attempts.push(`Konfiguration ohne gültigen executablePath: ${config}`);
        continue;
      }
      const found = executableFrom(data.executablePath);
      if (found) return found;
      attempts.push(`EXE aus Konfiguration nicht gefunden: ${data.executablePath}`);
    } catch (error) {
      attempts.push(`Konfiguration nicht lesbar: ${config} (${error instanceof Error ? error.message : String(error)})`);
    }
  }
  return undefined;
}
function registeredPath(): string | undefined {
  // Installer location may differ from Program Files; inspect uninstall registry keys.
  for (const root of ["HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall", "HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall", "HKLM\\Software\\WOW6432Node\\Microsoft\\Windows\\CurrentVersion\\Uninstall"]) {
    try {
      const keys = execFileSync("reg.exe", ["query", root, "/s"], { encoding: "utf8", windowsHide: true, timeout: 2500, maxBuffer: 4 * 1024 * 1024 });
      for (const section of keys.split(/(?=^HKEY_)/m)) {
        if (!/^\s*DisplayName\s+REG_SZ\s+Pace\s*Atlas\b/im.test(section)) continue;
        const location = section.match(/^\s*InstallLocation\s+REG_SZ\s+(.+)$/im)?.[1]?.trim();
        if (location) { const found = executableFrom(location); if (found) return found; }
        const displayIcon = section.match(/^\s*DisplayIcon\s+REG_SZ\s+(.+)$/im)?.[1]?.trim();
        if (displayIcon) {
          const found = executableFrom(displayIcon.replace(/,\d+$/, ""));
          if (found) return found;
        }
      }
    } catch { /* Registry key absent or inaccessible. */ }
  }
  return undefined;
}
function findApplication(): string {
  const attempts: string[] = [];
  const configured = getPreferenceValues<Preferences>().applicationPath?.trim();
  if (configured) {
    const found = executableFrom(configured);
    if (!found) throw new Error(`Der konfigurierte PaceAtlas-Pfad existiert nicht: ${configured}. Bitte in den Raycast-Erweiterungseinstellungen korrigieren.`);
    return found;
  }
  const installed = installerPath(attempts);
  if (installed) return installed;
  const bases = [process.env.ProgramW6432, process.env.ProgramFiles, process.env["ProgramFiles(x86)"],
    `${process.env.SystemDrive || "C:"}\\Program Files`,
    `${process.env.SystemDrive || "C:"}\\Program Files (x86)`,
    ...localAppDataCandidates()];
  for (const base of new Set(bases.filter((item): item is string => Boolean(item)))) {
    for (const sub of ["PaceAtlas", "Pace Atlas", "Programs\\PaceAtlas", "Programs\\Pace Atlas"]) {
      const candidate = path.join(base, sub);
      const found = executableFrom(candidate);
      if (found) return found;
      attempts.push(`EXE fehlt: ${path.join(candidate, "PaceAtlas.WinUI.exe")}`);
    }
  }
  const registered = registeredPath();
  if (registered) return registered;
  throw new Error(`PaceAtlas wurde nicht gefunden. Bitte den Anwendungspfad in den Raycast-Einstellungen hinterlegen. Diagnose:\n${attempts.join("\n")}`);
}
function probe(): Promise<boolean> {
  return new Promise(resolve => {
    const socket = net.createConnection(PIPE);
    let done = false;
    const finish = (ok: boolean) => { if (done) return; done = true; socket.destroy(); resolve(ok); };
    socket.once("connect", () => finish(true));
    socket.once("error", () => finish(false));
    socket.setTimeout(500, () => finish(false));
  });
}
async function launchAndWait(): Promise<void> {
  if (await probe()) return;
  const exe = findApplication();
  await new Promise<void>((resolve, reject) => {
    const child = spawn(exe, ["--tray"], { detached: true, stdio: "ignore", windowsHide: true, cwd: path.dirname(exe) });
    child.once("error", reject);
    child.once("spawn", () => { child.unref(); resolve(); });
  });
  const deadline = Date.now() + 20000;
  while (Date.now() < deadline) {
    if (await probe()) return;
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error("PaceAtlas wurde gestartet, aber die IPC-Schnittstelle war nach 20 Sekunden nicht erreichbar.");
}
export function ensurePaceAtlas(): Promise<void> {
  if (!pendingStart) pendingStart = launchAndWait().finally(() => { pendingStart = undefined; });
  return pendingStart;
}
