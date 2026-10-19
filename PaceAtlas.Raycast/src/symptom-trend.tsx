import { Action, ActionPanel, Color, Icon, List, Detail, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
import { alphabetic } from "./sort";

type Options = { symptoms: string[] };
type Point = { id: number; start: string; severity: number; label: string };
type Trend = { name: string; days: number; language: string; points: Point[] };
function TrendDetails({ name }: { name: string }) {
  const [trend, setTrend] = useState<Trend>();
  const [days, setDays] = useState(14);
  const [error, setError] = useState("");
  useEffect(() => {
    let alive = true;
    void call<Trend>("symptoms.trend", { name, days }).then(x => { if (alive) setTrend(x); }).catch(e => { if (alive) setError(String(e)); });
    return () => { alive = false; };
  }, [name, days]);
  const points = trend?.points ?? [];
  const lines = points.slice().reverse().map(p => `| ${new Date(p.start).toLocaleString(trend?.language === "en" ? "en-GB" : "de-DE")} | ${p.label} |`).join("\n");
  const md = error ? `# Fehler\n\n${error}` : !trend ? "Lade Symptomverlauf …" :
    `# ${name}\n\n**${points.length} bewertete Einträge in ${days} Tagen**\n\nNicht beurteilte Tage werden nicht als beschwerdefrei gewertet.\n\n| Zeitpunkt | Stärke |\n|---|---|\n${lines || "| Keine bewerteten Einträge | – |"}`;
  return <Detail isLoading={!trend && !error} markdown={md} actions={<ActionPanel>
    {[7,14,30,90].map(n => <Action key={n} title={`${n} Tage anzeigen`} onAction={() => { setTrend(undefined); setDays(n); }} />)}
  </ActionPanel>} />;
}
export default function SymptomTrend() {
  const { push } = useNavigation();
  const [names, setNames] = useState<string[]>([]);
  const [error, setError] = useState("");
  useEffect(() => { void call<Options>("status.options.list").then(o => setNames(alphabetic(o.symptoms, x => x))).catch(e => setError(String(e))); }, []);
  return <List searchBarPlaceholder="Symptom auswählen …" isLoading={!names.length && !error}>
    {names.map(name => <List.Item key={name} title={name} icon={{source:Icon.Plus,tintColor:Color.Red}}
      actions={<ActionPanel><Action title="Verlauf anzeigen" onAction={() => push(<TrendDetails name={name} />)} /></ActionPanel>} />)}
    {error && <List.Item title="Symptome konnten nicht geladen werden" subtitle={error} />}
  </List>;
}
