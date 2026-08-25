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
import { prepareUpload } from "@/lib/prepare-upload";
import { shareFile } from "@/lib/share";
import { DOCUMENT_TYPE_LABELS, type DocumentInfo, type DocumentType } from "@/lib/types";
import { FileUp, Download, Trash2, FileText } from "lucide-react";

interface UploadLimits {
  maxBytes: number;
  maxMb: number;
  accepted: string;
}

/** Only used if the limits call hasn't answered yet — the server stays the
 *  real authority, this just keeps the first upload from being unbounded. */
const DEFAULT_MAX_BYTES = 2.5 * 1024 * 1024;

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
  /** Null while the owner is still being created. Documents are stored
   *  against an id, so there is nothing to attach them to yet — the panel
   *  still renders, greyed out, so the capability is visible on the Add form
   *  rather than appearing from nowhere after the first save. */
  ownerId: string | null;
  types: readonly DocumentType[];
  emptyText: string;
}) {
  if (!ownerId) {
    return (
      <div className="space-y-2 rounded-lg border border-dashed p-3 opacity-70">
        <Label className="text-xs text-muted-foreground">Documents</Label>
        <p className="text-sm text-muted-foreground">
          Save first — then you can upload {types.slice(0, 2).map((t) => DOCUMENT_TYPE_LABELS[t].toLowerCase()).join(", ")} and
          more, right here.
        </p>
      </div>
    );
  }

  return <DocumentList ownerPath={ownerPath} ownerId={ownerId} types={types} emptyText={emptyText} />;
}

function DocumentList({
  ownerPath,
  ownerId,
  types,
  emptyText,
}: {
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

  // Machine configuration, not data: fetched once and kept, so the client can
  // turn away an oversized file before sending it up a mobile connection
  // without the limit being duplicated as a magic number here.
  const limitsQuery = useQuery({
    queryKey: ["documents", "limits"],
    queryFn: () => api.get<UploadLimits>("/api/documents/limits"),
    staleTime: Infinity,
    gcTime: Infinity,
  });
  const maxBytes = limitsQuery.data?.maxBytes ?? DEFAULT_MAX_BYTES;

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
    // Cleared straight away so picking the same file twice still fires change.
    e.target.value = "";
    if (!file) return;

    setBusy(true);
    setNote("");
    try {
      // A phone photo is 2-5 MB, well over the limit, so it is shrunk here
      // rather than refused. Only a file that still doesn't fit afterwards —
      // in practice an oversized PDF — is turned away, and it is turned away
      // before a byte goes over the network.
      const prepared = await prepareUpload(file, maxBytes);
      if (!prepared.ok) {
        setNote(prepared.reason);
        return;
      }

      await api.upload(`/api/${ownerPath}/${ownerId}/documents`, prepared.file, { documentType });
      toast.success(`${DOCUMENT_TYPE_LABELS[documentType]} uploaded.`);
      refresh();
    } catch (err) {
      setNote(
        err instanceof ApiError
          ? err.status === 413
            ? "That file is too large to upload."
            : err.message
          : "Couldn't upload that file. Check your connection and try again.",
      );
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

      {/* min-w-0 on both controls is load-bearing, not tidying. A grid item
          defaults to min-width:auto, so neither control could shrink below its
          own text, and the pair came to more than a 320px phone leaves inside
          the dialog. The panel then pushed the whole form wider than the card
          it sits in — the dialog itself had to stop stretching too, which is
          the matching [&>*]:min-w-0 in DialogContent. */}
      <div className="relative grid grid-cols-2 gap-2">
        <Select value={documentType} onValueChange={(v) => v && setDocumentType(v as DocumentType)}>
          <SelectTrigger className="h-11 w-full min-w-0">
            <SelectValue>
              {(v: DocumentType) => <span className="truncate">{DOCUMENT_TYPE_LABELS[v]}</span>}
            </SelectValue>
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
          className="h-11 min-w-0"
          disabled={busy}
          onClick={() => fileInput.current?.click()}
        >
          <FileUp className="h-4 w-4 shrink-0" />
          <span className="truncate">{busy ? "Uploading…" : "Upload"}</span>
        </Button>
        {/* Two things here are deliberate and both are about iOS Safari.

            The input is moved off-screen rather than given `hidden`:
            display:none stops Safari opening the picker at all when the input
            is clicked through a ref, which made Upload appear to do nothing on
            an iPhone while working everywhere else.

            `accept` is the two broad types rather than a long list of
            specific ones. Safari matches that list against what the Photos
            app can offer, and a list naming image/heic and extensions would
            leave photos greyed out and unpickable. image/* covers everything a
            camera produces, HEIC included.

            Either way accept is only a hint to the picker, never the rule —
            the server checks the file's actual leading bytes. */}
        <input
          ref={fileInput}
          type="file"
          accept="image/*,application/pdf"
          className="pointer-events-none absolute h-px w-px opacity-0"
          tabIndex={-1}
          aria-hidden="true"
          disabled={busy}
          onChange={onPick}
        />
      </div>

      {note && <p className="text-sm text-muted-foreground">{note}</p>}
    </div>
  );
}
