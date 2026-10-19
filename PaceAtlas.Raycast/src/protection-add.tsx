import { Action, ActionPanel, Form, Icon, List, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
import { alphabetic } from "./sort";

type Activity = { id: number; name: string; start: string };
type Options = { hearingProtection: string[] };

function AddProtection({ activity }: { activity: Activity }) {
  const { pop } = useNavigation();
  const [options, setOptions] = useState<string[]>([]);
  const [selected, setSelected] = useState<string[]>([]);
  useEffect(() => {
    void call<Options>("activities.options.list")
      .then(o => setOptions(alphabetic(o.hearingProtection, x => x)))
      .catch(e => void showToast({ style: Toast.Style.Failure, title: "Schutzmaßnahmen nicht verfügbar", message: String(e) }));
  }, []);
  async function save() {
    if (!selected.length) { await showToast({ style: Toast.Style.Failure, title: "Bitte Schutzmaßnahmen auswählen" }); return; }
    try {
      await call("activities.protection.add", { id: activity.id, protection: selected });
      await showToast({ style: Toast.Style.Success, title: "Schutzmaßnahmen ergänzt" });
      pop();
    } catch (e) { await showToast({ style: Toast.Style.Failure, title: "Speichern fehlgeschlagen", message: String(e) }); }
  }
  return <Form actions={<ActionPanel><Action title="Schutzmaßnahmen ergänzen" onAction={save} /></ActionPanel>}>
    <Form.Description title="Aktivität" text={activity.name} />
    <Form.TagPicker id="protection" title="Schutzmaßnahmen" value={selected} onChange={setSelected}>
      {options.map(p => <Form.TagPicker.Item key={p} value={p} title={p} />)}
    </Form.TagPicker>
  </Form>;
}

export default function ProtectionAdd() {
  const { push } = useNavigation();
  const [activities, setActivities] = useState<Activity[]>([]);
  const [error, setError] = useState("");
  useEffect(() => { void call<Activity[]>("activities.active.list").then(setActivities).catch(e => setError(String(e))); }, []);
  return <List searchBarPlaceholder="Laufende Aktivität auswählen">
    {activities.map(a => <List.Item key={a.id} icon={Icon.Shield} title={a.name || `Aktivität ${a.id}`}
      actions={<ActionPanel><Action title="Schutzmaßnahmen auswählen" onAction={() => push(<AddProtection activity={a} />)} /></ActionPanel>} />)}
    {!activities.length && !error && <List.EmptyView title="Keine laufenden Aktivitäten" />}
    {error && <List.Item title="Fehler" subtitle={error} />}
  </List>;
}
