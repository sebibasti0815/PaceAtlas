import { Action, ActionPanel, Icon, List } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Summary = { language: string; stateRecorded: boolean; overall?: number; pem?: number; crash?: boolean; activities: number; activityMinutes: number; restEntries: number; restMinutes: number; activeActivities: number; activeRest: number; pendingMedications: number };
export default function Day() {
  const [data, setData] = useState<Summary>(); const [error, setError] = useState("");
  async function reload() { try { setData(await call<Summary>("day.summary")); setError(""); } catch(e) { setError(String(e)); } }
  useEffect(() => { void reload(); }, []);
  const en = data?.language === "en";
  const overall = [en ? "good" : "gut", en ? "slightly impaired" : "leicht eingeschränkt", en ? "moderate" : "mittel", en ? "poor" : "schlecht", en ? "very poor" : "sehr schlecht"];
  return <List isLoading={!data && !error}>
    {data && <>
      <List.Item icon={Icon.Heart} title={en ? "Latest condition today" : "Letzter Zustand heute"} subtitle={data.stateRecorded ? `${overall[data.overall ?? -1] ?? "–"} · PEM: ${data.pem ?? "–"} · Crash: ${data.crash ? "Ja" : "Nein"}` : (en ? "Not recorded" : "Nicht erfasst")} />
      <List.Item icon={Icon.Person} title={en ? "Activities" : "Aktivitäten"} subtitle={`${data.activities} · ${data.activityMinutes} min · ${data.activeActivities} laufend`} />
      <List.Item icon={Icon.Moon} title={en ? "Rest" : "Ruhe"} subtitle={`${data.restEntries} · ${data.restMinutes} min · ${data.activeRest} laufend`} />
      <List.Item icon={Icon.Circle} title={en ? "Pending medication" : "Offene Medikamente"} subtitle={String(data.pendingMedications)} />
    </>}
    {error && <List.Item title="PaceAtlas nicht erreichbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload}/></ActionPanel>} />}
  </List>;
}
