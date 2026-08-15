"use client";

import { useMemo, useState } from "react";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { Input } from "@/components/ui/input";
import { ChevronDown, Plus, Search } from "lucide-react";
import { cn } from "@/lib/utils";

export interface PickerOption {
  id: string;
  label: string;
  sublabel?: string;
}

/**
 * A typable/searchable picker for a mobile-sized list — a full-screen bottom
 * sheet with a search box, standing in for a plain <Select> wherever the
 * list is long enough that scanning it beats scanning a dropdown. Only ever
 * resolves to an existing option's id; `onAddNew` (optional) is the escape
 * hatch for "the thing I want isn't here yet" — the caller decides what
 * happens next (typically opening its own quick-add form), this component
 * never invents a record on its own.
 */
export function SearchablePicker({
  label,
  placeholder = "Choose",
  options,
  value,
  onSelect,
  onAddNew,
  addNewLabel = "Add",
}: {
  label: string;
  placeholder?: string;
  options: PickerOption[];
  value: string;
  onSelect: (id: string) => void;
  onAddNew?: (searchText: string) => void;
  addNewLabel?: string;
}) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState("");

  const selected = options.find((o) => o.id === value);
  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    if (!q) return options;
    return options.filter(
      (o) => o.label.toLowerCase().includes(q) || o.sublabel?.toLowerCase().includes(q),
    );
  }, [options, query]);

  const exactMatch = filtered.some((o) => o.label.toLowerCase() === query.trim().toLowerCase());

  return (
    <>
      <button
        type="button"
        onClick={() => { setQuery(""); setOpen(true); }}
        className="flex h-12 w-full items-center justify-between rounded-md border border-input bg-transparent px-3 text-left text-base"
      >
        <span className={cn("truncate", !selected && "text-muted-foreground")}>
          {selected?.label ?? placeholder}
        </span>
        <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
      </button>

      <Sheet open={open} onOpenChange={setOpen}>
        <SheetContent side="bottom" className="flex h-[85vh] flex-col rounded-t-3xl pb-[env(safe-area-inset-bottom)]">
          <SheetHeader><SheetTitle>{label}</SheetTitle></SheetHeader>
          <div className="px-4">
            <div className="relative">
              <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                autoFocus
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={`Search ${label.toLowerCase()}…`}
                className="h-12 pl-9 text-base"
              />
            </div>
          </div>
          <div className="flex-1 overflow-y-auto px-4 py-2">
            {onAddNew && query.trim().length > 0 && !exactMatch && (
              <button
                type="button"
                onClick={() => { onAddNew(query.trim()); setOpen(false); }}
                className="mb-1 flex min-h-12 w-full items-center gap-2 rounded-lg border border-dashed border-primary/50 px-3 py-2 text-left text-sm font-medium text-primary"
              >
                <Plus className="h-4 w-4 shrink-0" /> {addNewLabel} &quot;{query.trim()}&quot;
              </button>
            )}
            <div className="space-y-1">
              {filtered.map((o) => (
                <button
                  key={o.id}
                  type="button"
                  onClick={() => { onSelect(o.id); setOpen(false); }}
                  className={cn(
                    "flex min-h-12 w-full flex-col items-start justify-center rounded-lg px-3 py-2 text-left transition hover:bg-accent",
                    o.id === value && "bg-accent",
                  )}
                >
                  <span className="truncate text-sm font-medium">{o.label}</span>
                  {o.sublabel && <span className="truncate text-xs text-muted-foreground">{o.sublabel}</span>}
                </button>
              ))}
              {filtered.length === 0 && !onAddNew && (
                <p className="py-4 text-center text-sm text-muted-foreground">No matches.</p>
              )}
            </div>
          </div>
        </SheetContent>
      </Sheet>
    </>
  );
}
