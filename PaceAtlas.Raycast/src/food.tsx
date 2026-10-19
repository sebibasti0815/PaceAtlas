import { Action, ActionPanel, Icon, List, Detail, useNavigation } from "@raycast/api";
import { useEffect, useRef, useState } from "react";
import { call } from "./ipc";

type FoodSummary = { id: number; name: string; glycemicLoadPer100G?: number | null };
type Food = FoodSummary & { carbsPer100G?: number | null; glycemicIndex?: number | null; decision?: string | null; ruleNote?: string | null; source?: string };
const label = (decision?: string | null) => ({ exclude: "Verboten", avoid: "Vermeiden", conditional: "Bedingt", allow: "Erlaubt" })[decision ?? ""] ?? "Keine Regel";
const num = (n?: number | null) => n == null ? "Nicht bekannt" : n.toLocaleString("de-DE", { maximumFractionDigits: 1 });

function FoodDetails({ id }: { id: number }) {
  const [food, setFood] = useState<Food | null>(null);
  const [error, setError] = useState("");
  useEffect(() => {
    let active = true;
    void call<Food>("food.get", { id }).then(f => { if (active) setFood(f); }).catch(e => { if (active) setError(String(e)); });
    return () => { active = false; };
  }, [id]);
  const markdown = error ? `# Fehler\n\n${error}` : !food ? "Lade Lebensmittel …" :
    `# ${food.name}\n\n**Einstufung:** ${label(food.decision)}\n\n**GL pro 100 g:** ${num(food.glycemicLoadPer100G)}\n\n**Kohlenhydrate pro 100 g:** ${num(food.carbsPer100G)}\n\n**GI:** ${num(food.glycemicIndex)}\n\n**Regelhinweis:** ${food.ruleNote || "–"}\n\n**Quelle:** ${food.source || "–"}\n\n*Die glykämische Last einer Portion hängt von ihrer Größe ab.*`;
  return <Detail isLoading={!food && !error} markdown={markdown} />;
}

export default function FoodCheck() {
  const { push } = useNavigation();
  const [query, setQuery] = useState("");
  const [foods, setFoods] = useState<FoodSummary[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  const generation = useRef(0);
  useEffect(() => {
    const current = ++generation.current;
    const text = query.trim();
    if (text.length < 2) { setFoods([]); setError(""); setLoading(false); return; }
    setLoading(true); setError("");
    const timer = setTimeout(() => {
      void call<FoodSummary[]>("food.search", { query: text, limit: 30 })
        .then(items => { if (generation.current === current) setFoods(items); })
        .catch(e => { if (generation.current === current) { setError(String(e)); setFoods([]); } })
        .finally(() => { if (generation.current === current) setLoading(false); });
    }, 300);
    return () => { clearTimeout(timer); generation.current++; };
  }, [query]);
  return <List isLoading={loading} searchText={query} onSearchTextChange={setQuery} filtering={false} searchBarPlaceholder="Lebensmittel prüfen …">
    {query.trim().length < 2 && <List.EmptyView title="Lebensmittel suchen" description="Mindestens zwei Zeichen eingeben." />}
    {query.trim().length >= 2 && !loading && !error && foods.length === 0 && <List.EmptyView title="Keine Treffer" />}
    {foods.map(f => <List.Item key={f.id} title={f.name} icon={Icon.Leaf} accessories={[{ text: f.glycemicLoadPer100G == null ? "GL unbekannt" : `GL/100 g: ${num(f.glycemicLoadPer100G)}` }]} actions={<ActionPanel><Action title="Details anzeigen" onAction={() => push(<FoodDetails id={f.id} />)} /></ActionPanel>} />)}
    {error && <List.Item title="Lebensmittel konnten nicht geladen werden" subtitle={error} />}
  </List>;
}
