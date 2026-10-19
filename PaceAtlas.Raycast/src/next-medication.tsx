import { Action, ActionPanel, Color, Icon, List, showToast, Toast } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Plan = { id: number; name: string; time: string; dose: string; quantity: string; form: string; overdue: boolean };
type Response = { pending: number; overdue: number; next: Plan[] };
export default function NextMedication() {
  const [data, setData] = useState<Response>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  async function reload() {
    setLoading(true);
    try { setData(await call<Response>("medications.next")); setError(""); }
    catch (e) { setError(String(e)); }
    finally { setLoading(false); }
  }
  useEffect(() => { void reload(); }, []);
  async function confirm(id: number) {
    try {
      await call("medications.confirm", { planId: id });
      await showToast({ style: Toast.Style.Success, title: "Einnahme bestätigt" });
      await reload();
    } catch (e) { await showToast({ style: Toast.Style.Failure, title: "Fehler", message: String(e) }); }
  }
  return <List isLoading={loading} searchBarPlaceholder="Offene Einnahmen suchen">
    {data && <List.Section title={`${data.pending} offen · ${data.overdue} überfällig`}>
      {data.next.map(p => <List.Item key={`${p.id}-${p.time}`} title={p.name}
        subtitle={`${p.time} · ${p.dose} · ${p.quantity} ${p.form}`}
        icon={{ source: p.overdue ? Icon.ExclamationMark : Icon.Clock, tintColor: p.overdue ? Color.Red : Color.SecondaryText }}
        accessories={[{ text: p.overdue ? "Überfällig" : "Anstehend" }]}
        actions={<ActionPanel><Action title="Als eingenommen bestätigen" onAction={() => confirm(p.id)} />
          <Action title="Aktualisieren" onAction={reload} /></ActionPanel>} />)}
      {!data.next.length && <List.Item title="Keine offenen Einnahmen" icon={Icon.CheckCircle} />}
    </List.Section>}
    {error && <List.Item title="Einnahmen konnten nicht geladen werden" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload} /></ActionPanel>} />}
  </List>;
}
