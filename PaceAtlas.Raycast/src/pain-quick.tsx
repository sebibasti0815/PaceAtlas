import { Action, ActionPanel, Form, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";

type Options = { painLocations: string[] };
type Values = { overall: string; pem: string; crash: boolean; painLocations: string[]; note: string };
export default function PainQuick() {
  const { pop } = useNavigation();
  const [locations, setLocations] = useState<string[]>([]);
  const [error, setError] = useState("");
  useEffect(() => { void call<Options>("status.options.list")
    .then(r => setLocations([...r.painLocations].sort((a,b) => a.localeCompare(b, "de"))))
    .catch(e => setError(String(e))); }, []);
  async function save(v: Values) {
    if (error || locations.length === 0) {
      await showToast({ style: Toast.Style.Failure, title: "Schmerzorte konnten nicht geladen werden" }); return;
    }
    if (!v.painLocations?.length) {
      await showToast({ style: Toast.Style.Failure, title: "Bitte mindestens einen Schmerzort auswählen" }); return;
    }
    try {
      await call("status.create", {
        captureMode: "Quick", overall: Number(v.overall), pem: Number(v.pem), crash: v.crash,
        painLocations: v.painLocations, symptoms: {}, pulse: null, note: v.note ?? "",
      });
      await showToast({ style: Toast.Style.Success, title: "Schmerzerfassung gespeichert" }); pop();
    } catch (e) { await showToast({ style: Toast.Style.Failure, title: "Speichern fehlgeschlagen", message: String(e) }); }
  }
  return <Form actions={<ActionPanel><Action.SubmitForm title="Schmerzen erfassen" onSubmit={save} /></ActionPanel>}>
    <Form.Description text="Regulärer Zustandseintrag mit Schmerzorten. Symptome bleiben nicht beurteilt, Puls bleibt ohne Angabe." />
    {error && <Form.Description text={error} />}
    <Form.Dropdown id="overall" title="Allgemeinzustand" defaultValue="2">
      {["gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht"].map((label, n) =>
        <Form.Dropdown.Item key={n} value={String(n)} title={label} />)}
    </Form.Dropdown>
    <Form.Dropdown id="pem" title="PEM" defaultValue="0">
      <Form.Dropdown.Item value="0" title="Keine" />
      <Form.Dropdown.Item value="1" title="Vermutet" />
      <Form.Dropdown.Item value="2" title="Ja" />
    </Form.Dropdown>
    <Form.Checkbox id="crash" title="Crash" label="Crash vorhanden" />
    <Form.TagPicker id="painLocations" title="Schmerzorte" info="Mehrfachauswahl möglich">
      {locations.map(name => <Form.TagPicker.Item key={name} value={name} title={name} />)}
    </Form.TagPicker>
    <Form.TextArea id="note" title="Notizen" placeholder="Optional" />
  </Form>;
}
