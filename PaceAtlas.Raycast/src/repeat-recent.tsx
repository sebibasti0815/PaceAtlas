import { Action, ActionPanel, Form, Icon, List, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type RecentMeasure = { id: number; name: string; note: string; reasons: string[]; start: string };
function ConfirmRepeat({ item }: { item: RecentMeasure }) {
  const { pop } = useNavigation();
  const [saving, setSaving] = useState(false);
  async function submit(values: { note: string }) {
    setSaving(true);
    try {
      await call("measures.create", { name: item.name, reasons: item.reasons, note: values.note });
      await showToast({ style: Toast.Style.Success, title: "Maßnahme erneut erfasst" }); pop();
    } catch (error) {
      await showToast({ style: Toast.Style.Failure, title: "Wiederholen fehlgeschlagen", message: String(error) });
    } finally { setSaving(false); }
  }
  return <Form isLoading={saving} actions={<ActionPanel><Action.SubmitForm title="Maßnahme jetzt erfassen" onSubmit={submit}/></ActionPanel>}>
    <Form.Description title="Maßnahme" text={item.name}/>
    <Form.Description title="Gründe" text={item.reasons.join(", ") || "Keine"}/>
    <Form.TextArea id="note" title="Notiz" defaultValue={item.note} info="Erzeugt einen neuen Eintrag mit aktuellem Zeitpunkt. Der alte Eintrag bleibt erhalten."/>
  </Form>;
}
export default function RepeatRecent() {
  const [items, setItems] = useState<RecentMeasure[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  useEffect(() => { void call<RecentMeasure[]>("measures.recent.list")
    .then(setItems).catch(e => setError(String(e))).finally(() => setLoading(false)); }, []);
  return <List isLoading={loading} searchBarPlaceholder="Maßnahme suchen">
    {items.map(item => <List.Item key={item.id} icon={Icon.ArrowClockwise} title={item.name} subtitle={item.start.replace("T", " ")}
      actions={<ActionPanel><Action.Push title="Erneut erfassen" target={<ConfirmRepeat item={item}/>} /></ActionPanel>}/>)}
    {error && <List.Item title="Maßnahmen konnten nicht geladen werden" subtitle={error}/>}
    {!loading && !error && !items.length && <List.EmptyView title="Keine bisherigen Maßnahmen"/>}
  </List>;
}
