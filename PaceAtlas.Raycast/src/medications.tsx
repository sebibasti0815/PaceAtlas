import { Action, ActionPanel, Alert, confirmAlert, List, Icon, Color, showToast, Toast } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Plan = { id: number; name: string; time: string; dose: string; quantity: string; form: string };
type Taken = Plan & { planId: number };
export default function Medications() {
  const [plans, setPlans] = useState<Plan[]>([]); const [taken, setTaken] = useState<Taken[]>([]); const [error, setError] = useState("");
  async function reload() { try { const [p,t] = await Promise.all([call<Plan[]>("medications.pending.list"),call<Taken[]>("medications.taken.list")]); setPlans(p); setTaken(t); setError(""); } catch(e) { setError(String(e)); } }
  useEffect(() => { void reload(); }, []);
  async function confirm(id: number) { try { await call("medications.confirm", {planId:id}); await showToast({style:Toast.Style.Success,title:"Einnahme bestätigt"}); await reload(); } catch(e) { await showToast({style:Toast.Style.Failure,title:"Fehler",message:String(e)}); } }
  async function undo(id: number) {
    if (!await confirmAlert({ title: "Einnahme zurücknehmen?", message: "Die Einnahme wird gelöscht und die Bestandsberechnung aktualisiert.", primaryAction: { title: "Als nicht eingenommen markieren", style: Alert.ActionStyle.Destructive } })) return;
    try { await call("medications.undo", {intakeId:id}); await showToast({style:Toast.Style.Success,title:"Einnahme zurückgenommen"}); await reload(); }
    catch(e) { await showToast({style:Toast.Style.Failure,title:"Fehler",message:String(e)}); }
  }
  return <List searchBarPlaceholder="Medikamente suchen">
    <List.Section title="Offen">{plans.map(p => <List.Item icon={{source: Icon.Circle, tintColor: Color.Orange}} key={`open-${p.id}-${p.time}`} title={p.name} subtitle={`${p.time} · ${p.dose} · ${p.quantity} ${p.form}`} actions={<ActionPanel><Action title="Als genommen bestätigen" onAction={() => confirm(p.id)}/></ActionPanel>}/>)}</List.Section>
    <List.Section title="Heute eingenommen">{taken.map(p => <List.Item icon={{source: Icon.CheckCircle, tintColor: Color.Green}} key={`taken-${p.id}`} title={p.name} subtitle={`${p.time} · ${p.dose} · ${p.quantity} ${p.form}`} accessories={[{text:"Eingenommen"}]} actions={<ActionPanel><Action title="Als nicht eingenommen markieren" onAction={() => undo(p.id)}/></ActionPanel>}/>)}</List.Section>
    {error && <List.Item title="PaceAtlas nicht erreichbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload}/></ActionPanel>}/>}
  </List>;
}
