"use client";

import { useRef, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { api, ApiError } from "@/lib/api";
import { shareFile } from "@/lib/share";
import { DOCUMENT_TYPE_LABELS, type DocumentInfo, type DocumentType } from "@/lib/types";
import { FileUp, Download, Trash2, FileText } from "lucide-react";

/**
 * The documents held against one vehicle or one driver — a list, plus an
 * upload that asks what kind of paper it is.
 *
 * Shared by both tabs rather than written twice: the two differ only in which
 * document types they offer and which endpoint they list from, and the
 * fiddly parts (the empty state, a file whose bytes have gone missing, the
 * size limit, share-vs-download on a phone) are worth having in one place.
 *
 * Every document is optional. A vehicle or driver with nothing uploaded is a
 * normal record, so the empty state is a quiet line rather than a warning.
 */
export function DocumentPanel({
  ownerPath,
  ownerId,
  types,
  emptyText,
}: {
  /** "vehicles" or "drivers" — the API segment its documents hang off. */
  ownerPath: "vehicles" | "drivers";
  ownerId: string;
  types: readonly DocumentType[];
  emptyText: string;
}) {
  const queryClient = useQueryClient();
  const fileInput = useRef<HTMLInputElement>(null);
  const [documentType, setDocumentType] = useState<DocumentType>(types[0]);
  const [busy, setBusy] = useState(false);
  const [note, setNote] = useState("");

  const key = ["documents", ownerPath, ownerId];

  const docsQuery = useQuery({
    queryKey: key,
    // An owner with nothing on file returns an empty list, not 204 — but keep
    // the ?? [] so a bodyless response can never resolve as undefined, which
    // TanStack Query treats as a failed query rather than a value.
    queryFn: async () => (await api.get<DocumentInfo[]>(`/api/${ownerPath}/${ownerId}/documents`)) ?? [],
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: key });

  const removeMutation = useMutation({
    mutationFn: (documentId: string) => api.delete(`/api/documents/${documentId}`),
    onSuccess: () => {
      toast.success("Document removed.");
      refresh();
    },
    onError: () => toast.error("Couldn't remove that document."),
  });

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    setBusy(true);
    setNote("");
    try {
      await api.upload(`/api/${ownerPath}/${ownerId}/documents`, file, { documentType });
      toast.success(`${DOCUMENT_TYPE_LABELS[documentType]} uploaded.`);
      refresh();
    } catch (err) {
      setNote(err instanceof ApiError ? err.message : "Couldn't upload that file.");
    } finally {
      setBusy(false);
    }
  }

  async function onOpen(doc: DocumentInfo) {
    setNote("");
    try {
      const file = await api.getFile(`/api/documents/${doc.id}/download`, doc.fileName);
      const outcome = await shareFile(file, { title: doc.fileName });
      if (outcome === "downloaded") toast.success("Downloaded — open it from your downloads.");
    } catch (err) {
      // A 404 means the row is there but the file isn't — say so plainly and
      // let them re-upload, rather than showing a failure.
      setNote(
        err instanceof ApiError && err.status === 404
          ? "That document is no longer available. Upload it again."
          : "Couldn't open that document.",
      );
      refresh();
    }
  }

  const documents = docsQuery.data ?? [];

  return (
    <div className="space-y-3 rounded-lg border p-3">
      <Label className="text-xs text-muted-foreground">Documents</Label>

      {documents.length > 0 ? (
        <ul className="space-y-2">
          {documents.map((doc) => (
            <li key={doc.id} className="flex items-center gap-2 rounded-lg bg-muted/50 p-2">
              <FileText className="h-4 w-4 shrink-0 text-muted-foreground" />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium">{doc.documentTypeLabel}</p>
                <p className="truncate text-xs text-muted-foreground">
                  {doc.fileName} · {(doc.sizeBytes / (1024 * 1024)).toFixed(1)} MB
                </p>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Open ${doc.documentTypeLabel}`}
                onClick={() => void onOpen(doc)}
              >
                <Download className="h-4 w-4" />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label={`Remove ${doc.documentTypeLabel}`}
                disabled={removeMutation.isPending}
                onClick={() => removeMutation.mutate(doc.id)}
              >
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        !docsQuery.isLoading && <p className="text-sm text-muted-foreground">{emptyText}</p>
      )}

      <div className="grid grid-cols-2 gap-2">
        <Select value={documentType} onValueChange={(v) => v && setDocumentType(v as DocumentType)}>
          <SelectTrigger className="h-11 w-full">
            <SelectValue>{(v: DocumentType) => DOCUMENT_TYPE_LABELS[v]}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            {types.map((t) => (
              <SelectItem key={t} value={t}>
                {DOCUMENT_TYPE_LABELS[t]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Button
          type="button"
          variant="outline"
          className="h-11"
          disabled={busy}
          onClick={() => fileInput.current?.click()}
        >
          <FileUp className="h-4 w-4" /> {busy ? "Uploading…" : "Upload"}
        </Button>
        <input ref={fileInput} type="file" className="hidden" disabled={busy} onChange={onPick} />
      </div>

      {note && <p className="text-sm text-muted-foreground">{note}</p>}
    </div>
  );
}
