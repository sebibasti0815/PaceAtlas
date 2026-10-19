import { Action, ActionPanel, Form, showToast, Toast, useNavigation } from "@raycast/api";
import { call } from "./ipc";
export default function Notes() {
  const { pop } = useNavigation();
  async function save(values: { text: string }) {
    try { await call("notes.create", { text: values.text }); await showToast({ style: Toast.Style.Success, title: "Notiz gespeichert" }); pop(); }
    catch (e) { await showToast({ style: Toast.Style.Failure, title: "Speichern fehlgeschlagen", message: String(e) }); }
  }
  return <Form actions={<ActionPanel><Action.SubmitForm title="Schnellnotiz speichern" onSubmit={save}/></ActionPanel>}>
    <Form.Description text="Datum und Uhrzeit werden automatisch gesetzt." />
    <Form.TextArea id="text" title="Notiz" placeholder="Was möchtest du festhalten?" />
  </Form>;
}
