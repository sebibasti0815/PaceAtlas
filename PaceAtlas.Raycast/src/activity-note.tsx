import { Action, ActionPanel, Form, List, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Activity = { id: number; name: string; start: string };
function NoteForm({ activity }: { activity: Activity }) {
  const {pop} = useNavigation();
  async function submit(v: {note:string}) {
    if (!v.note.trim()) { await showToast({style:Toast.Style.Failure,title:"Notiz fehlt"}); return; }
    try { await call("activities.note.append", {id:activity.id,note:v.note}); await showToast({style:Toast.Style.Success,title:"Notiz ergänzt"}); pop(); }
    catch(e) { await showToast({style:Toast.Style.Failure,title:"Fehler",message:String(e)}); }
  }
  return <Form actions={<ActionPanel><Action.SubmitForm title="Notiz ergänzen" onSubmit={submit}/></ActionPanel>}>
    <Form.Description title="Aktivität" text={activity.name}/><Form.TextArea id="note" title="Neue Notiz" />
  </Form>;
}
export default function ActivityNote() {
  const [activities,setActivities]=useState<Activity[]>([]); const [error,setError]=useState(""); const {push}=useNavigation();
  useEffect(() => { void call<Activity[]>("activities.active.list").then(setActivities).catch(e=>setError(String(e))); }, []);
  return <List searchBarPlaceholder="Laufende Aktivität wählen …">
    {activities.map(a=><List.Item key={a.id} title={a.name} subtitle={a.start.replace("T"," ")} actions={<ActionPanel><Action title="Notiz ergänzen" onAction={()=>push(<NoteForm activity={a}/>)} /></ActionPanel>}/>)}
    {!activities.length && !error && <List.EmptyView title="Keine laufenden Aktivitäten"/>}
    {error && <List.Item title="Fehler" subtitle={error}/>}
  </List>;
}
