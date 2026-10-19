import { Action, ActionPanel, Icon, List, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
import RestCommand from "./rest";
import PacingCommand from "./pacing";
import Day from "./day";
import Activities, { CustomActivity } from "./activities";
import RepeatRecent from "./repeat-recent";
import Status from "./status";
import Medications from "./medications";
import Measures from "./measures";
import History from "./history";
import Notes from "./notes";
import FoodCheck from "./food";
import SymptomTrend from "./symptom-trend";
import NextMedication from "./next-medication";
import ActivityNote from "./activity-note";
import PacingLoad from "./pacing-load";
import ProtectionRecent from "./protection-recent";
import ProtectionAdd from "./protection-add";
import PainQuick from "./pain-quick";
import PemContext from "./pem-context";
type Active = { id: number; name: string; start: string };
type Rest = { id: number; start: string };
type Context = { activities: Active[]; rests: Rest[]; pendingMedications: number; lastEntries: { id: number; kind: string; title: string; start: string }[] };
export default function Quick() {
  const { push } = useNavigation(); const [context, setContext] = useState<Context>(); const [error, setError] = useState("");
  async function reload() { try { setContext(await call<Context>("quick.context")); setError(""); } catch(e) { setError(String(e)); } }
  useEffect(() => { void reload(); }, []);
  async function transition(id: number) {
    try { await call("activity.rest.transition", { activityId: id }); await showToast({ style: Toast.Style.Success, title: "Aktivität beendet und Ruhe begonnen" }); await reload(); }
    catch(e) { await showToast({ style: Toast.Style.Failure, title: "Übergang fehlgeschlagen", message: String(e) }); }
  }
  return <List searchBarPlaceholder="Schnellaktionen suchen">
    <List.Section title="Angepinnt">
      <List.Item key="pinned-status" icon={Icon.Plus} title="Zustand erfassen" actions={<ActionPanel><Action title="Zustand erfassen" onAction={() => push(<Status/>)} /></ActionPanel>} />
      <List.Item key="pinned-activity" icon={Icon.Play} title="Aktivität erfassen" subtitle="Eigene Aktivität oder Vorlage" actions={<ActionPanel><Action title="Aktivität erfassen" onAction={() => push(<CustomActivity onSaved={reload}/>)} /></ActionPanel>} />
    </List.Section>
    <List.Section title="Weitere Schnellaktionen">
    <List.Item icon={Icon.Person} title="Aktivitäten" actions={<ActionPanel><Action title="Aktivitäten öffnen" onAction={() => push(<Activities/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Pencil} title="Aktivitätsnotiz ergänzen" actions={<ActionPanel><Action title="Notiz ergänzen" onAction={() => push(<ActivityNote/>)} /></ActionPanel>} />
    <List.Item icon={Icon.BarChart} title="Belastungsübersicht (48 Stunden)" actions={<ActionPanel><Action title="Belastungen anzeigen" onAction={() => push(<PacingLoad/>)} /></ActionPanel>} />
    <List.Item icon={Icon.List} title="Bisheriger Verlauf" actions={<ActionPanel><Action title="Verlauf öffnen" onAction={() => push(<History/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Leaf} title="Lebensmittel prüfen" actions={<ActionPanel><Action title="Lebensmittel prüfen" onAction={() => push(<FoodCheck/>)} /></ActionPanel>} />
    <List.Item icon={Icon.ArrowClockwise} title="Letzte Maßnahme wiederholen" actions={<ActionPanel><Action title="Maßnahme auswählen" onAction={() => push(<RepeatRecent/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Circle} title="Medikamente bestätigen" actions={<ActionPanel><Action title="Medikamente öffnen" onAction={() => push(<Medications/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Clock} title="Offene Medikamenteneinnahmen" actions={<ActionPanel><Action title="Offene Einnahmen" onAction={() => push(<NextMedication/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Clock} title="Pacing-Timer" actions={<ActionPanel><Action title="Timer öffnen" onAction={() => push(<PacingCommand/>)} /></ActionPanel>} />
    <List.Item key="pem-context" icon={Icon.BarChart} title="PEM-Belastungshinweise" actions={<ActionPanel><Action title="Belastungshinweise öffnen" onAction={() => push(<PemContext/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Moon} title="Ruhephasen" subtitle="Ruhe starten oder beenden" actions={<ActionPanel><Action title="Ruhephasen öffnen" onAction={() => push(<RestCommand/>)} /></ActionPanel>} />
    <List.Item key="pain-quick" icon={Icon.Heart} title="Schmerzen schnell erfassen" actions={<ActionPanel><Action title="Schmerzen erfassen" onAction={() => push(<PainQuick/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Pencil} title="Schnellnotiz" actions={<ActionPanel><Action title="Notiz erfassen" onAction={() => push(<Notes/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Shield} title="Schutzmaßnahmen ergänzen" actions={<ActionPanel><Action title="Schutzmaßnahmen ergänzen" onAction={() => push(<ProtectionAdd/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Shield} title="Schutzmaßnahmen – Verlauf" actions={<ActionPanel><Action title="Schutzmaßnahmen anzeigen" onAction={() => push(<ProtectionRecent/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Heart} title="Sofortmaßnahme erfassen" actions={<ActionPanel><Action title="Sofortmaßnahmen öffnen" onAction={() => push(<Measures/>)} /></ActionPanel>} />
    <List.Item icon={Icon.Plus} title="Symptomverlauf" actions={<ActionPanel><Action title="Symptomverlauf öffnen" onAction={() => push(<SymptomTrend/>)} /></ActionPanel>} />
    <List.Item icon={Icon.List} title="Tagesstatus" actions={<ActionPanel><Action title="Tagesstatus öffnen" onAction={() => push(<Day/>)} /></ActionPanel>} />
    </List.Section>
    {context && <>
      <List.Section title="Laufende Aktivitäten">{context.activities.map(a => <List.Item key={a.id} icon={Icon.Play} title={a.name || `Aktivität ${a.id}`} subtitle="Beenden und Ruhe starten" actions={<ActionPanel><Action title="Aktivität beenden und Ruhe starten" onAction={() => transition(a.id)} /></ActionPanel>} />)}</List.Section>
      <List.Section title="Aktuell"> <List.Item title="Offene Medikamente" subtitle={String(context.pendingMedications)} icon={Icon.Circle} />
        <List.Item title="Laufende Ruhephasen" subtitle={String(context.rests.length)} icon={Icon.Moon} /></List.Section>
      <List.Section title="Zuletzt erfasst">{context.lastEntries.map(e => <List.Item key={e.id} title={e.title} subtitle={`${e.kind} · ${e.start.replace("T", " ")}`} />)}</List.Section>
    </>}
    {error && <List.Item title="PaceAtlas nicht erreichbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload}/></ActionPanel>} />}
  </List>;
}
