import { Action, ActionPanel, Color, Detail, Icon, List, showToast, Toast, useNavigation } from "@raycast/api";
import { useEffect, useRef, useState } from "react";
import { call } from "./ipc";

type Kind = "Zustand" | "Aktivität" | "Ruhe" | "Maßnahme" | "Notiz";
type Language = "de" | "en";
type Item = { id: number; kind: Kind; start: string; end?: string | null; title: string; summary: string; hasNote: boolean };
type Page = { language: Language; items: Item[]; total: number; nextOffset: number | null };
type Field = { label: string; value: string };
type Full = Item & { language: Language; kindLabel: string; note: string; fields: Field[] };
const iconFor = (kind: Kind) => {
  switch (kind) {
    case "Zustand": return { source: Icon.Plus, tintColor: Color.Red };
    case "Aktivität": return { source: Icon.Person, tintColor: Color.Blue };
    case "Ruhe": return { source: Icon.Moon, tintColor: Color.Purple };
    case "Maßnahme": return { source: Icon.Heart, tintColor: Color.Orange };
    case "Notiz": return { source: Icon.Pencil, tintColor: Color.SecondaryText };
  }
};
const stamp = (s: string, lang: Language) => {
  const m = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})/.exec(s);
  if (!m) return s;
  return lang === "en" ? `${m[2]}/${m[3]}/${m[1]} ${m[4]}:${m[5]}` : `${m[3]}.${m[2]}.${m[1]} ${m[4]}:${m[5]}`;
};
const intervalLabel = (entry: Full) => {
  const start = stamp(entry.start, entry.language);
  if (!entry.end) return `${start}${entry.kind === "Aktivität" || entry.kind === "Ruhe" ? ` · ${tr(entry.language, "läuft noch", "ongoing")}` : ""}`;
  const end = stamp(entry.end, entry.language);
  const sameDay = entry.start.slice(0, 10) === entry.end.slice(0, 10);
  return `${start} ${tr(entry.language, "bis", "to")} ${sameDay ? end.slice(-5) : end}`;
};
const escapeMd = (s: string) => s.replace(/[\\`*_{}\[\]()#+.!|>~-]/g, "\\$&");
const tr = (lang: Language, de: string, en: string) => lang === "en" ? en : de;
function EntryDetail({ id }: { id: number }) {
  const [entry, setEntry] = useState<Full>();
  const [error, setError] = useState("");
  useEffect(() => { void call<Full>("history.get", { id }).then(setEntry).catch(e => setError(String(e))); }, [id]);
  const markdown = entry ? [
    `# ${escapeMd(entry.title)}`,
    `**${escapeMd(entry.kindLabel)}** · ${escapeMd(intervalLabel(entry))}`,

    entry.summary ? `**${tr(entry.language, "Zusammenfassung", "Summary")}:** ${escapeMd(entry.summary)}` : "",
    `## ${tr(entry.language, "Angaben", "Details")}`,
    ...(entry.fields ?? []).map(field => `**${escapeMd(field.label)}:** ${escapeMd(field.value)}`),
    entry.note ? `## ${tr(entry.language, "Notizen", "Notes")}\n${escapeMd(entry.note)}` : "",
  ].filter(Boolean).join("\n\n") : error ? `# Fehler\n${escapeMd(error)}` : "Eintrag wird geladen …";
  return <Detail isLoading={!entry && !error} markdown={markdown} />;
}
export default function History() {
  const { push } = useNavigation();
  const [items, setItems] = useState<Item[]>([]);
  const [language, setLanguage] = useState<Language>("de");
  const [filter, setFilter] = useState("all");
  const [next, setNext] = useState<number | null>(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [searchText, setSearchText] = useState("");
  const pending = useRef(false);
  const nextRef = useRef<number | null>(0);
  const autoRequestedFor = useRef<number | null>(null);
  const initialSelection = useRef(true);
  async function load(offset: number, reset = false) {
    if (pending.current) return;
    pending.current = true;
    setLoading(true);
    try {
      const page = await call<Page>("history.list", { offset, limit: 30 });
      setLanguage(page.language === "en" ? "en" : "de");
      setItems(previous => reset ? page.items : [...previous, ...page.items.filter(x => !previous.some(p => p.id === x.id))]);
      if (reset) {
        initialSelection.current = true;
        autoRequestedFor.current = null;
        setSelectedId(page.items.length ? `${page.items[0].kind}:${page.items[0].id}` : null);
      }
      nextRef.current = page.nextOffset;
      setNext(page.nextOffset);
      setError("");
    } catch (e) {
      setError(String(e));
      await showToast({ style: Toast.Style.Failure, title: "Verlauf konnte nicht geladen werden", message: String(e) });
    } finally { pending.current = false; setLoading(false); }
  }
  useEffect(() => { void load(0, true); }, []);
  const visible = items.filter(x => filter === "all" || x.kind === filter);
  const visibleMatching = visible.filter(x => `${x.title} ${x.summary} ${stamp(x.start, language)}`.toLocaleLowerCase().includes(searchText.toLocaleLowerCase()));
  function selectItem(id: string | null) {
    setSelectedId(id);
    if (id === "__more") return;
    if (initialSelection.current) {
      initialSelection.current = false;
      // Initial focus should remain on the newest entry.
      if (id === `${visibleMatching[0]?.kind}:${visibleMatching[0]?.id}`) return;
    }
    // Fetch the next page when keyboard or mouse selection approaches the bottom.
    // A per-offset guard prevents repeated requests while the selection remains there.
    const index = visibleMatching.findIndex(x => `${x.kind}:${x.id}` === id);
    const offset = nextRef.current;
    if (offset !== null && index >= 0 && index >= visibleMatching.length - 5 && !pending.current && autoRequestedFor.current !== offset) {
      autoRequestedFor.current = offset;
      void load(offset);
    }
  }
  function loadMoreManually() {
    const offset = nextRef.current;
    if (offset === null || pending.current) return;
    const last = visibleMatching.at(-1);
    if (last) setSelectedId(`${last.kind}:${last.id}`);
    void load(offset);
  }
  useEffect(() => {
    if (!visible.length) { setSelectedId(null); return; }
    if (!selectedId || !visible.some(x => `${x.kind}:${x.id}` === selectedId))
      setSelectedId(`${visible[0].kind}:${visible[0].id}`);
  }, [filter, items, selectedId]);
  return <List isLoading={loading} selectedItemId={selectedId ?? undefined} onSelectionChange={selectItem} onSearchTextChange={setSearchText} searchBarPlaceholder={tr(language, "Bisherigen Verlauf durchsuchen …", "Search history …")}
    searchBarAccessory={<List.Dropdown tooltip={tr(language, "Eintragstyp", "Entry type")} value={filter} onChange={setFilter}>
      <List.Dropdown.Item title={tr(language, "Alle Einträge", "All entries")} value="all" />
      {(["Zustand", "Aktivität", "Ruhe", "Maßnahme", "Notiz"] as Kind[]).map((k, n) => <List.Dropdown.Item key={k} title={language === "en" ? ["Condition", "Activity", "Rest", "Intervention", "Quick note"][n] : k} value={k} />)}
    </List.Dropdown>}>
    {error && <List.Item title="Fehler beim Laden" subtitle={error} icon={Icon.Warning} />}
    {visible.map(item => <List.Item key={`${item.kind}:${item.id}`} id={`${item.kind}:${item.id}`} icon={iconFor(item.kind)} title={item.title}
      subtitle={[stamp(item.start, language), item.summary].filter(Boolean).join(" · ")} accessories={item.hasNote ? [{ icon: Icon.Text, tooltip: tr(language, "Mit Notiz", "Has note") }] : []}
      actions={<ActionPanel>
        <Action title={tr(language, "Details anzeigen", "Show details")} icon={Icon.Eye} onAction={() => push(<EntryDetail id={item.id} />)} />
        <Action title={tr(language, "Aktualisieren", "Refresh")} icon={Icon.ArrowClockwise} onAction={() => void load(0, true)} />
      </ActionPanel>} />)}
    {next !== null && <List.Item key="__more" id="__more" title={tr(language, "Weitere 30 Einträge laden", "Load 30 more entries")} icon={Icon.ChevronDown}
      actions={<ActionPanel><Action title={tr(language, "Ältere Einträge laden", "Load older entries")} onAction={loadMoreManually} /></ActionPanel>} />}
  </List>;
}
