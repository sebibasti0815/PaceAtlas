import { alphabetic } from "./sort";
import { Action, ActionPanel, Form, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";

type Options = { measures: string[]; reasons: string[] };
export default function Measures() {
  const [options, setOptions] = useState<Options>({ measures: [], reasons: [] });
  const [loading, setLoading] = useState(true);
  const { pop } = useNavigation();
  useEffect(() => {
    void call<Options>("measures.options.list").then(data => setOptions({ ...data, measures: alphabetic(data.measures, x => x), reasons: alphabetic(data.reasons, x => x) }))
      .catch((e) => showToast({ style: Toast.Style.Failure, title: "Optionen konnten nicht geladen werden", message: String(e) }))
      .finally(() => setLoading(false));
  }, []);
  async function submit(values: { name: string; reasons: string[]; note: string }) {
    try {
      await call("measures.create", { name: values.name, reasons: values.reasons ?? [], note: values.note ?? "" });
      await showToast({ style: Toast.Style.Success, title: "Sofortmaßnahme gespeichert" });
      pop();
    } catch (e) {
      await showToast({ style: Toast.Style.Failure, title: "Speichern fehlgeschlagen", message: String(e) });
    }
  }
  return <Form isLoading={loading} actions={<ActionPanel><Action.SubmitForm title="Sofortmaßnahme speichern" onSubmit={submit}/></ActionPanel>}>
    <Form.Description text="Datum und Uhrzeit werden beim Speichern automatisch übernommen." />
    <Form.Dropdown id="name" title="Maßnahme">
      {options.measures.map((name) => <Form.Dropdown.Item key={name} value={name} title={name}/>)}
    </Form.Dropdown>
    <Form.TagPicker id="reasons" title="Gründe">
      {options.reasons.map((reason) => <Form.TagPicker.Item key={reason} value={reason} title={reason}/>)}
    </Form.TagPicker>
    <Form.TextArea id="note" title="Notizen" placeholder="Optional" />
  </Form>;
}
