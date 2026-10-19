import { Action, ActionPanel, Icon, List } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Activity = { id: number; start: string; minutes: number; intensity: number | null; dimensions: string[] };
type Result = { count: number; totalMinutes: number; activities: Activity[] };
export default function PemContext() {
  const [data,setData] = useState<Result>(); const [error,setError] = useState("");
  async function reload() { try { setData(await call<Result>("pacing.load.summary")); setError(""); } catch(e) { setError(String(e)); } }
  useEffect(() => { void reload(); }, []);
  return <List isLoading={!data && !error} searchBarPlaceholder="Belastungen durchsuchen">
    <List.Section title="Dokumentierte Belastung – letzte 48 Stunden">
      <List.Item key="disclaimer" icon={Icon.Info} title="Keine PEM-Risikoprognose" subtitle="Nur dokumentierte Aktivitäten; fehlende Einträge sind keine Ruhezeiten." />
      {data && <List.Item key="total" icon={Icon.BarChart} title={`${data.count} Aktivitäten · ${data.totalMinutes} Minuten`} subtitle="Deskriptive Übersicht, keine individuelle medizinische Vorhersage" actions={<ActionPanel><Action title="Aktualisieren" onAction={reload}/></ActionPanel>}/>}
      {data?.activities.map(a => <List.Item key={a.id} icon={Icon.Clock} title={a.dimensions.join(", ") || `Aktivität ${a.id}`} subtitle={`${a.start.replace("T", " ").slice(0,16)} · ${a.minutes} Minuten · Intensität ${a.intensity ?? "unbekannt"}`}/>)}
      {error && <List.Item key="error" title="Belastungen nicht verfügbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload}/></ActionPanel>}/>}
    </List.Section>
  </List>;
}
