const collator = new Intl.Collator("de-DE", { sensitivity: "base", numeric: true });
export function alphabetic<T>(items: readonly T[], label: (item: T) => string): T[] {
  return [...items].sort((a, b) => collator.compare(label(a), label(b)));
}
