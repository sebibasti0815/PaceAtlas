import { Action, ActionPanel, Form, Icon, List, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";

type Rest = { id: number; start: string; note: string };
const hhmm = (d: Date) => `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
const localStamp = (time: string) => {
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) throw new Error("Uhrzeit muss HH:mm sein");
  const d = new Date();
  return `${d.getFullYear()}-${String(d.getMonth()+1).padStart(2,"0")}-${String(d.getDate()).padStart(2,"0")}T${time}`;
};
function RestForm({ onSaved, current }: { onSaved: () => Promise<void>; current?: Rest }) {
  const { pop } = useNavigation();
  const [time, setTime] = useState(hhmm(new Date()));
  async function submit(values: { note: string }) {
    try {
      const stamp = localStamp(time);
      await call(current ? "rest.stop" : "rest.start", current ? { id: current.id, end: stamp, note: values.note } : { start: stamp, note: values.note });
      await showToast({ style: Toast.Style.Success, title: current ? "Ruhe beendet" : "Ruhe gestartet" });
      pop(); await onSaved();
    } catch (error) { await showToast({ style: Toast.Style.Failure, title: "Ruhe konnte nicht gespeichert werden", message: String(error) }); }
  }
  return <Form actions={<ActionPanel><Action.SubmitForm title={current ? "Ruhe beenden" : "Ruhe starten"} onSubmit={submit}/></ActionPanel>}>
    <Form.TextField id="time" title={current ? "Ende" : "Beginn"} value={time} onChange={setTime} info="Heute, lokale Uhrzeit (HH:mm)" />
    <Form.TextArea id="note" title="Notizen" placeholder="Optional" />
  </Form>;
}
export default function RestCommand() {
  const { push } = useNavigation();
  const [rests, setRests] = useState<Rest[]>([]); const [error, setError] = useState("");
  async function reload() { try { setRests(await call<Rest[]>("rest.active.list")); setError(""); } catch(e) { setError(String(e)); } }
  useEffect(() => { void reload(); }, []);
  return <List searchBarPlaceholder="Ruhephasen">
    <List.Item title="Ruhe beginnen …" icon={Icon.Plus} actions={<ActionPanel><Action title="Ruhe beginnen" onAction={() => push(<RestForm onSaved={reload}/>)} /></ActionPanel>} />
    <List.Section title="Laufende Ruhephasen">{rests.map(r => <List.Item key={r.id} title={r.note?.split("\n")[0] || "Ruhe"} subtitle={r.start.replace("T", " ")} icon={Icon.Moon} actions={<ActionPanel><Action title="Ruhe beenden …" onAction={() => push(<RestForm current={r} onSaved={reload}/>)} /></ActionPanel>} />)}</List.Section>
    {error && <List.Item title="PaceAtlas nicht erreichbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload}/></ActionPanel>} />}
  </List>;
}
