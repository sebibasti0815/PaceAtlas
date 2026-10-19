import { Action, ActionPanel, Icon, List } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Item = { id: number; start: string; minutes: number; intensity: number | null; dimensions: string[]; protection: string[] };
type Response = { count: number; totalMinutes: number; activities: Item[]; disclaimer: string };
export default function PacingLoad() {
 const [data,setData] = useState<Response>(); const [error,setError] = useState("");
 async function reload() { try { setData(await call<Response>("pacing.load.summary")); setError(""); } catch(e) { setError(String(e)); } }
 useEffect(() => { void reload(); }, []);
 return <List isLoading={!data && !error} searchBarPlaceholder="Belastungen durchsuchen">
  {data && <List.Section title={`Letzte 48 Stunden · ${data.count} Aktivitäten · ${data.totalMinutes} Minuten dokumentiert`}>
   <List.Item icon={Icon.Info} title="Keine PEM-Prognose" subtitle={data.disclaimer} />
   {data.activities.map(a => <List.Item key={a.id} icon={Icon.Clock} title={a.dimensions.join(", ") || `Aktivität ${a.id}`}
    subtitle={`${a.start.replace("T", " ").slice(0,16)} · ${a.minutes} Min. · Intensität ${a.intensity ?? "unbekannt"}`}
    accessories={a.protection.length ? [{text:a.protection.join(", ")}] : []}
    actions={<ActionPanel><Action title="Aktualisieren" onAction={reload}/></ActionPanel>} />)}
  </List.Section>}
  {error && <List.Item title="Belastungsübersicht nicht verfügbar" subtitle={error} />}
 </List>;
}
