import { alphabetic } from "./sort";
import { Action, ActionPanel, Form, List, Icon, Color, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Template = { name: string; intensity: number; dimensions: string[]; hearingProtection: string[] };
type Active = { id: number; name: string; note: string; displayNote?: string; start: string };
const activityTitle = (a: Active) => {
  const name = a.name || `Aktivität ${a.id}`;
  const note = (a.displayNote ?? a.note ?? "").replace(/\s+/g, " ").trim();
  if (!note || note.toLocaleLowerCase("de-DE") === name.toLocaleLowerCase("de-DE")) return name;
  const abbreviated = note.length > 100 ? `${note.slice(0, 99).trimEnd()}…` : note;
  return `${name} (${abbreviated})`;
};
const displayTime = (value: string) => {
  const match = /T(\d{2}:\d{2})/.exec(value);
  return match?.[1] ?? value;
};
type Options = { dimensions: string[]; hearingProtection: string[] };
const pad = (n: number) => String(n).padStart(2, "0");
const localTime = (d: Date) => `${pad(d.getHours())}:${pad(d.getMinutes())}`;
const todayDateTime = (time: string) => {
  if (!/^([01]\d|2[0-3]):[0-5]\d$/.test(time)) return null;
  const now = new Date();
  const result = new Date(now.getFullYear(), now.getMonth(), now.getDate(), Number(time.slice(0, 2)), Number(time.slice(3, 5)));
  return { date: result, isoLocal: `${now.getFullYear()}-${pad(now.getMonth()+1)}-${pad(now.getDate())}T${time}` };
};
export function CustomActivity({ onSaved }: { onSaved: () => Promise<void> }) {
  const { pop } = useNavigation();
  const [completed, setCompleted] = useState(false);
  const [templates, setTemplates] = useState<Template[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState("__custom__");
  const [name, setName] = useState("");
  const [intensity, setIntensity] = useState("1");
  const [dimensions, setDimensions] = useState<string[]>([]);
  const [hearingProtection, setHearingProtection] = useState<string[]>([]);
  const [options, setOptions] = useState<Options>({ dimensions: [], hearingProtection: [] });
  const [start, setStart] = useState(localTime(new Date()));
  const [end, setEnd] = useState(localTime(new Date()));
  useEffect(() => {
    void Promise.all([call<Options>("activities.options.list"), call<Template[]>("activities.templates.list")])
      .then(([opts, list]) => { setOptions({ dimensions: alphabetic(opts.dimensions, x => x), hearingProtection: alphabetic(opts.hearingProtection, x => x) }); setTemplates(alphabetic(list, x => x.name)); })
      .catch(e => { void showToast({style: Toast.Style.Failure, title: "Aktivitätsdaten konnten nicht geladen werden", message: String(e)}); });
  }, []);
  function chooseTemplate(value: string) {
    setSelectedTemplate(value);
    if (value === "__custom__") return; // Manuelle Änderungen bleiben bei Rückkehr erhalten.
    const template = templates.find(t => t.name === value);
    if (!template) return;
    setName(template.name);
    setIntensity(String(template.intensity));
    setDimensions([...template.dimensions]);
    setHearingProtection([...template.hearingProtection]);
  }
  async function submit(v: { note: string }) {
    if (!name.trim()) { await showToast({ style: Toast.Style.Failure, title: "Bitte eine Bezeichnung eingeben" }); return; }
    const startParsed = todayDateTime(start); const endParsed = todayDateTime(end);
    if (!startParsed || (completed && !endParsed)) { await showToast({style: Toast.Style.Failure, title: "Uhrzeit im Format HH:mm eingeben"}); return; }
    if (completed && endParsed!.date < startParsed.date) { await showToast({style: Toast.Style.Failure, title: "Ende liegt vor Beginn"}); return; }
    try {
      await call("activities.custom.create", { name: name.trim(), intensity: Number(intensity), completed, start: startParsed.isoLocal, end: completed ? endParsed!.isoLocal : null, dimensions, hearingProtection, note: v.note });
      await showToast({ style: Toast.Style.Success, title: "Aktivität gespeichert" }); pop(); await onSaved();
    } catch (e) { await showToast({ style: Toast.Style.Failure, title: "Speichern fehlgeschlagen", message: String(e) }); }
  }
  return <Form actions={<ActionPanel><Action.SubmitForm title="Aktivität speichern" onSubmit={submit}/></ActionPanel>}>
    <Form.Dropdown id="template" title="Aktivitätsvorlage" value={selectedTemplate} onChange={chooseTemplate}>
      <Form.Dropdown.Item value="__custom__" title="Eigene Aktivität" />
      {templates.map(t => <Form.Dropdown.Item key={t.name} value={t.name} title={t.name} />)}
    </Form.Dropdown>
    <Form.TextField id="name" title="Aktivität" placeholder="Was machst du?" value={name} onChange={setName} />
    <Form.Dropdown id="intensity" title="Intensität" value={intensity} onChange={setIntensity}>
      {["gering", "mittel", "hoch", "sehr hoch"].map((label, n) => <Form.Dropdown.Item key={n} value={String(n)} title={label}/>) }
    </Form.Dropdown>
    <Form.TagPicker id="dimensions" title="Belastungsarten" value={dimensions} onChange={setDimensions}>
      {options.dimensions.map(value => <Form.TagPicker.Item key={value} value={value} title={value}/>)}
    </Form.TagPicker>
    <Form.TagPicker id="hearingProtection" title="Schutzmaßnahmen" value={hearingProtection} onChange={setHearingProtection}>
      {options.hearingProtection.map(value => <Form.TagPicker.Item key={value} value={value} title={value}/>)}
    </Form.TagPicker>
    <Form.TextArea id="note" title="Notizen" placeholder="Optional" />
    <Form.TextField id="startTime" title="Beginn" value={start} onChange={setStart} info="Heute, lokale Uhrzeit (HH:mm)"/>
    <Form.Checkbox id="completed" title="Status" label="Bereits abgeschlossen" value={completed} onChange={setCompleted}/>
    {completed && <Form.TextField id="endTime" title="Ende" value={end} onChange={setEnd} info="Heute, lokale Uhrzeit (HH:mm)"/>}
  </Form>;
}
function ActivityNote({ title, onSave }: { title: string; onSave: (note: string) => Promise<void> }) {
  const { pop } = useNavigation();
  return <Form actions={<ActionPanel><Action.SubmitForm title={title} onSubmit={async (v: { note: string }) => { await onSave(v.note ?? ""); pop(); }}/></ActionPanel>}>
    <Form.TextArea id="note" title="Notizen" placeholder="Optional" />
  </Form>;
}
function StopActivityForm({ activity, onStopped }: { activity: Active; onStopped: () => Promise<void> }) {
  const { pop } = useNavigation();
  const [endTime, setEndTime] = useState(() => localTime(new Date()));
  const [note, setNote] = useState("");
  const [saving, setSaving] = useState(false);
  async function submit() {
    const parsed = todayDateTime(endTime);
    if (!parsed) { await showToast({ style: Toast.Style.Failure, title: "Endzeit im Format HH:mm eingeben" }); return; }
    const started = new Date(activity.start);
    if (!Number.isNaN(started.getTime()) && parsed.date < started) {
      await showToast({ style: Toast.Style.Failure, title: "Endzeit liegt vor dem Beginn" }); return;
    }
    if (parsed.date > new Date(Date.now() + 60_000)) {
      await showToast({ style: Toast.Style.Failure, title: "Endzeit darf nicht in der Zukunft liegen" }); return;
    }
    setSaving(true);
    try {
      await call("activities.stop", { id: activity.id, end: parsed.isoLocal, note });
      await showToast({ style: Toast.Style.Success, title: "Aktivität beendet" });
      pop();
      await onStopped();
    } catch (e) {
      await showToast({ style: Toast.Style.Failure, title: "Beenden fehlgeschlagen", message: String(e) });
    } finally { setSaving(false); }
  }
  return <Form isLoading={saving} actions={<ActionPanel><Action.SubmitForm title="Aktivität beenden" onSubmit={submit}/></ActionPanel>}>
    <Form.Description title="Aktivität" text={activityTitle(activity)} />
    <Form.TextField id="endTime" title="Endzeit" value={endTime} onChange={setEndTime} info="Heute, lokale Uhrzeit (HH:mm); mit der Uhrzeit beim Öffnen vorbelegt" />
    <Form.TextArea id="note" title="Notizen ergänzen" placeholder="Optional" value={note} onChange={setNote} />
  </Form>;
}
export default function Activities() {
  const [templates, setTemplates] = useState<Template[]>([]); const [active, setActive] = useState<Active[]>([]); const [error, setError] = useState("");
  async function reload() { try { const [t,a] = await Promise.all([call<Template[]>("activities.templates.list"),call<Active[]>("activities.active.list")]); setTemplates(alphabetic(t, x => x.name)); setActive(a); setError(""); } catch(e) { setError(String(e)); } }
  useEffect(() => { void reload(); }, []);
  async function run(method: string, params: object) { try { await call(method, params); await showToast({style:Toast.Style.Success,title:"Gespeichert"}); await reload(); } catch(e) { await showToast({style:Toast.Style.Failure,title:"Fehler",message:String(e)}); } }
  const create = <Action.Push title="Aktivität erfassen" target={<CustomActivity onSaved={reload}/>}/>;
  return <List searchBarPlaceholder="Aktivitäten suchen">
    <List.Item icon={Icon.Plus} title="Aktivität erfassen…" subtitle="Eigene Aktivität oder Vorlage auswählen" actions={<ActionPanel>{create}</ActionPanel>}/>
    <List.Section title="Laufende Aktivitäten">{active.map(a => <List.Item icon={{ source: Icon.Play, tintColor: Color.Green }} key={a.id} title={activityTitle(a)} subtitle={displayTime(a.start)} actions={<ActionPanel><Action.Push title="Beenden" target={<StopActivityForm activity={a} onStopped={reload}/>}/>{create}</ActionPanel>} />)}</List.Section>
    <List.Section title="Vorlagen">{templates.map(t => <List.Item icon={Icon.List} key={t.name} title={t.name} subtitle={`Intensität ${["gering", "mittel", "hoch", "sehr hoch"][t.intensity] ?? t.intensity}`} actions={<ActionPanel><Action.Push title="Starten" target={<ActivityNote title="Aktivität starten" onSave={(note) => run("activities.start", {template:t.name, note})}/>}/>{create}</ActionPanel>} />)}</List.Section>
    {error && <List.Item title="PaceAtlas nicht erreichbar" subtitle={error} actions={<ActionPanel><Action title="Erneut versuchen" onAction={reload}/></ActionPanel>}/>}
  </List>;
}
