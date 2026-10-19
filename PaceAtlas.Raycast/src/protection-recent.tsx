import { Action, ActionPanel, Icon, List } from "@raycast/api";
import { useEffect, useState } from "react";
import { call } from "./ipc";
type Item = { id: number; kind: string; start: string; protection: string[] };
export default function ProtectionRecent() {
 const [items,setItems] = useState<Item[]>(); const [error,setError] = useState("");
 async function reload() { try { const r = await call<{items:Item[]}>("protection.recent.list"); setItems(r.items); setError(""); } catch(e) { setError(String(e)); } }
 useEffect(() => { void reload(); }, []);
 return <List isLoading={!items && !error} searchBarPlaceholder="Schutzmaßnahmen suchen">
  <List.Section title="Dokumentierte Schutzmaßnahmen · letzte 7 Tage">
   {items?.map(x => <List.Item key={x.id} icon={Icon.Shield} title={x.protection.join(", ")}
    subtitle={`${x.kind} · ${x.start.replace("T", " ").slice(0,16)}`}
    actions={<ActionPanel><Action title="Aktualisieren" onAction={reload}/></ActionPanel>} />)}
   {items?.length === 0 && <List.Item title="Keine Schutzmaßnahmen dokumentiert" />}
  </List.Section>
  {error && <List.Item title="Schutzmaßnahmen nicht verfügbar" subtitle={error} />}
 </List>;
}
