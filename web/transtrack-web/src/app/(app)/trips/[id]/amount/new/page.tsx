"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import type { PaymentMode, ReceiptType } from "@/lib/types";
import { cn } from "@/lib/utils";
import { ArrowLeft, Banknote, Landmark, Smartphone, FileText } from "lucide-react";

const empty = "00000000-0000-0000-0000-000000000000";
const quickAmounts = [1000, 2000, 5000, 10000, 20000];

const paymentModes: { value: PaymentMode; label: string; icon: typeof Banknote }[] = [
  { value: "Cash", label: "Cash", icon: Banknote },
  { value: "Bank", label: "Bank", icon: Landmark },
  { value: "Upi", label: "UPI", icon: Smartphone },
  { value: "Cheque", label: "Cheque", icon: FileText },
];

// Payment first, not Advance: it's the more common entry (a settlement
// against freight already delivered), and matches the field's own default
// on the server — picking the same one here means someone who taps straight
// through without looking at this row still gets the same answer either way.
const receiptTypes: { value: ReceiptType; label: string; hint: string }[] = [
  { value: "Payment", label: "Payment", hint: "Toward the settlement" },
  { value: "Advance", label: "Advance", hint: "Up front, against freight" },
];

export default function AddAmountPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();

  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState("");
  const [paymentMode, setPaymentMode] = useState<PaymentMode>("Cash");
  const [receiptType, setReceiptType] = useState<ReceiptType>("Payment");
  const [remarks, setRemarks] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/trips/${params.id}/transactions`, {
        id: empty,
        date,
        amount: Number(amount) || 0,
        paymentMode,
        receiptType,
        remarks: remarks || null,
      }),
    onSuccess: () => {
      toast.success("Amount recorded — waiting for approval.");
      queryClient.invalidateQueries({ queryKey: ["trips", params.id] });
      router.push(`/trips/${params.id}`);
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <div className="mx-auto max-w-lg p-4 pb-28 sm:p-6">
      <div className="mb-5 flex items-center gap-2">
        <Button variant="ghost" size="icon" onClick={() => router.push(`/trips/${params.id}`)}>
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <h1 className="text-xl font-semibold">Add amount received</h1>
      </div>

      <form
        className="space-y-6"
        onSubmit={(e) => {
          e.preventDefault();
          setError("");
          mutation.mutate();
        }}
      >
        <div className="space-y-2">
          <Label className="text-base">Date</Label>
          <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} className="h-12 text-base" />
        </div>

        <div className="space-y-2">
          <Label htmlFor="amount" className="text-base">Amount</Label>
          <Input
            id="amount"
            type="number"
            inputMode="decimal"
            placeholder="0"
            value={amount}
            onChange={(e) => setAmount(e.target.value)}
            className="h-14 text-2xl font-semibold"
            required
          />
          <div className="flex flex-wrap gap-2 pt-1">
            {quickAmounts.map((a) => (
              <button
                key={a}
                type="button"
                onClick={() => setAmount(String(a))}
                className="min-h-11 rounded-full border px-5 py-2.5 text-base font-medium transition hover:bg-accent desktop:min-h-0 desktop:px-3.5 desktop:py-1.5 desktop:text-sm"
              >
                ₹{a.toLocaleString("en-IN")}
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-2">
          <Label className="text-base">Advance or payment?</Label>
          <div className="grid grid-cols-2 gap-2">
            {receiptTypes.map((t) => (
              <button
                key={t.value}
                type="button"
                onClick={() => setReceiptType(t.value)}
                className={cn(
                  "rounded-2xl border-2 p-3 text-left transition",
                  receiptType === t.value
                    ? "border-primary bg-accent text-accent-foreground"
                    : "border-border bg-card hover:bg-accent/50",
                )}
              >
                <span className="block text-sm font-semibold">{t.label}</span>
                <span className="block text-xs text-muted-foreground">{t.hint}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-2">
          <Label className="text-base">How was it paid?</Label>
          <div className="grid grid-cols-4 gap-2">
            {paymentModes.map((m) => (
              <button
                key={m.value}
                type="button"
                onClick={() => setPaymentMode(m.value)}
                className={cn(
                  "flex flex-col items-center gap-1.5 rounded-2xl border-2 p-3 text-center transition",
                  paymentMode === m.value
                    ? "border-primary bg-accent text-accent-foreground"
                    : "border-border bg-card hover:bg-accent/50",
                )}
              >
                <m.icon className="h-5 w-5" />
                <span className="text-sm font-medium">{m.label}</span>
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-2">
          <Label className="text-base">Remarks (optional)</Label>
          <Textarea value={remarks} onChange={(e) => setRemarks(e.target.value)} className="text-base" />
        </div>

        {error && <p className="text-sm font-medium text-destructive">{error}</p>}

        <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={mutation.isPending || !amount}>
          {mutation.isPending ? "Adding…" : "Add amount"}
        </Button>
      </form>
    </div>
  );
}
