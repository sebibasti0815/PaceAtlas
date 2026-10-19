import { Action, ActionPanel, Icon, List, showToast, Toast } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Pacing = { running: boolean; resting: boolean; activeMinutes: number; pauseMinutes: number; remainingSeconds: number };
export default function PacingCommand() {
  const [state, setState] = useState<Pacing>(); const [error, setError] = useState("");
  async function refresh() { try { setState(await call<Pacing>("pacing.command", { action: "status" })); setError(""); } catch(e) { setError(String(e)); } }
  async function execute(action: string) { try { setState(await call<Pacing>("pacing.command", { action })); } catch(e) { await showToast({ style: Toast.Style.Failure, title: "Pacing-Aktion fehlgeschlagen", message: String(e) }); } }
  useEffect(() => { void refresh(); const interval = setInterval(() => { void refresh(); }, 10000); return () => clearInterval(interval); }, []);
  const duration = state ? `${Math.floor(state.remainingSeconds / 60)}:${String(state.remainingSeconds % 60).padStart(2,"0")}` : "–";
  return <List>
    <List.Item icon={Icon.Clock} title={state ? state.running ? state.resting ? "Pause läuft" : "Aktivität läuft" : "Timer aus" : "Lade Timerstatus …"} subtitle={state ? `Verbleibend: ${duration} · Aktiv ${state.activeMinutes} Min. / Pause ${state.pauseMinutes} Min.` : ""} actions={<ActionPanel><Action title="Aktualisieren" onAction={refresh}/></ActionPanel>} />
    {state && <List.Item title={state.running ? "Timer stoppen" : "Timer starten"} icon={state.running ? Icon.Stop : Icon.Play} actions={<ActionPanel><Action title={state.running ? "Timer stoppen" : "Timer starten"} onAction={() => execute(state.running ? "stop" : "start")}/></ActionPanel>} />}
    {state?.running && <List.Item title={state.resting ? "Pause beenden" : "Pause beginnen"} icon={Icon.Moon} actions={<ActionPanel><Action title={state.resting ? "Pause beenden" : "Pause beginnen"} onAction={() => execute("toggleBreak")}/></ActionPanel>} />}
    {error && <List.Item title="Pacing nicht erreichbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={refresh}/></ActionPanel>} />}
  </List>;
}
