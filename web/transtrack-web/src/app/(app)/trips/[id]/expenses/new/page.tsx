"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import type { ExpenseCategory } from "@/lib/types";
import { cn } from "@/lib/utils";
import { ArrowLeft } from "lucide-react";
import { iconForCategory } from "@/lib/expense-icons";

const empty = "00000000-0000-0000-0000-000000000000";
const quickAmounts = [100, 200, 500, 1000, 2000];

export default function AddExpensePage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();

  const categoriesQuery = useQuery({
    queryKey: ["expense-categories"],
    queryFn: () => api.get<ExpenseCategory[]>("/api/masters/expense-categories"),
  });

  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [categoryId, setCategoryId] = useState("");
  const [amount, setAmount] = useState("");
  const [remarks, setRemarks] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/trips/${params.id}/expenses`, {
        id: empty,
        date,
        expenseCategoryId: categoryId,
        amount: Number(amount) || 0,
        remarks: remarks || null,
      }),
    onSuccess: () => {
      toast.success("Expense added.");
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
        <h1 className="text-xl font-semibold">Add expense</h1>
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
          <Label className="text-base">What was it for?</Label>
          <div className="grid grid-cols-3 gap-2">
            {categoriesQuery.data?.map((c) => {
              const Icon = iconForCategory(c.name);
              return (
                <button
                  key={c.id}
                  type="button"
                  onClick={() => setCategoryId(c.id)}
                  className={cn(
                    "flex flex-col items-center gap-1.5 rounded-2xl border-2 p-3 text-center transition",
                    categoryId === c.id
                      ? "border-primary bg-accent text-accent-foreground"
                      : "border-border bg-card hover:bg-accent/50",
                  )}
                >
                  <Icon className="h-5 w-5" />
                  <span className="text-sm font-medium">{c.name}</span>
                </button>
              );
            })}
          </div>
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
                ₹{a}
              </button>
            ))}
          </div>
        </div>

        <div className="space-y-2">
          <Label className="text-base">Remarks (optional)</Label>
          <Textarea value={remarks} onChange={(e) => setRemarks(e.target.value)} className="text-base" />
        </div>

        {error && <p className="text-sm font-medium text-destructive">{error}</p>}

        <Button
          type="submit"
          size="lg"
          className="h-12 w-full text-base"
          disabled={mutation.isPending || !categoryId || !amount}
        >
          {mutation.isPending ? "Adding…" : "Add expense"}
        </Button>
      </form>
    </div>
  );
}
