import { alphabetic } from "./sort";
import { Action, ActionPanel, Form, Icon, Color, showToast, Toast } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";

type Options = { symptoms: string[]; painLocations: string[] };
const severities = [
  { value: "-1", label: "nicht beurteilt" },
  { value: "0", label: "keine" },
  { value: "1", label: "leicht" },
  { value: "2", label: "mittel" },
  { value: "3", label: "stark" },
  { value: "4", label: "extrem" },
];
export default function Status() {
  const [options, setOptions] = useState<Options>({ symptoms: [], painLocations: [] });
  const [selected, setSelected] = useState<string[]>([]);
  const [ratings, setRatings] = useState<Record<string, string>>({});
  const [limiting, setLimiting] = useState("");
  const [loadError, setLoadError] = useState("");
  useEffect(() => {
    void call<Options>("status.options.list").then(data => setOptions({ ...data, symptoms: alphabetic(data.symptoms, x => x), painLocations: alphabetic(data.painLocations, x => x) })).catch(e => {
      setLoadError(String(e));
      void showToast({ style: Toast.Style.Failure, title: "Symptomoptionen konnten nicht geladen werden", message: String(e) });
    });
  }, []);
  const ratedSymptoms = selected.filter(name => Number(ratings[name] ?? "2") > 0);
  async function submit(v: { overall: string; pem: string; crash: boolean; note: string; painLocations: string[] }) {
    if (loadError || !options.symptoms.length) {
      await showToast({ style: Toast.Style.Failure, title: "Bitte zunächst die Symptomoptionen laden" }); return;
    }
    const symptoms: Record<string, number> = {};
    for (const name of selected) symptoms[name] = Number(ratings[name] ?? "2");
    try {
      await call("status.create", {
        overall: Number(v.overall), pem: Number(v.pem), crash: v.crash, note: v.note,
        symptoms, painLocations: v.painLocations,
        mostLimitingSymptom: limiting && ratedSymptoms.includes(limiting) ? limiting : null,
      });
      await showToast({ style: Toast.Style.Success, title: "Zustand gespeichert" });
    } catch (e) { await showToast({ style: Toast.Style.Failure, title: "Speichern fehlgeschlagen", message: String(e) }); }
  }
  return <Form actions={<ActionPanel><Action.SubmitForm title="Zustand speichern" onSubmit={submit} /></ActionPanel>}>
    <Form.Dropdown id="overall" title="Allgemeinzustand" defaultValue="2">{["gut", "leicht eingeschränkt", "mittel", "schlecht", "sehr schlecht"].map((label, n) => <Form.Dropdown.Item key={n} value={String(n)} title={label} />)}</Form.Dropdown>
    <Form.Dropdown id="pem" title="PEM" defaultValue="0"><Form.Dropdown.Item value="0" title="Keine"/><Form.Dropdown.Item value="1" title="Vermutet"/><Form.Dropdown.Item value="2" title="Ja"/></Form.Dropdown>
    <Form.Checkbox id="crash" title="Crash" label="Crash vorhanden" />
    <Form.Separator />
    <Form.TagPicker id="symptomSelection" title="Symptome" value={selected} onChange={names => {
      setSelected(names);
      if (!names.includes(limiting)) setLimiting("");
    }} info="Wähle die vorhandenen Symptome. Nicht ausgewählte Symptome bleiben unbewertet.">
      {options.symptoms.map(name => <Form.TagPicker.Item key={name} value={name} title={name} icon={{ source: Icon.Plus, tintColor: Color.Red }} />)}
    </Form.TagPicker>
    {selected.map(name => <Form.Dropdown key={name} id={`symptom-${name}`} title={name} value={ratings[name] ?? "2"} onChange={value => {
      setRatings(old => ({ ...old, [name]: value }));
      if (limiting === name && Number(value) <= 0) setLimiting("");
    }}>{severities.map(item => <Form.Dropdown.Item key={item.value} value={item.value} title={item.label} />)}</Form.Dropdown>)}
    <Form.Dropdown id="mostLimiting" title="Am stärksten einschränkend" value={limiting} onChange={setLimiting} info="Welches Symptom schränkt dich gerade am meisten ein?">
      <Form.Dropdown.Item value="" title="Keine Angabe" />
      {ratedSymptoms.map(name => <Form.Dropdown.Item key={name} value={name} title={name} />)}
    </Form.Dropdown>
    <Form.Separator />
    <Form.TagPicker id="painLocations" title="Schmerzen / Schmerzorte" info="Wähle alle zutreffenden Schmerzorte.">
      {options.painLocations.map(name => <Form.TagPicker.Item key={name} value={name} title={name} />)}
    </Form.TagPicker>
    <Form.TextArea id="note" title="Notiz" />
  </Form>;
}
