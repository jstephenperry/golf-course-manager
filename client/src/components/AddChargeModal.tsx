import { useState } from "react";
import { LEDGER_CHARGE_CATEGORIES } from "../data/types";
import { Modal } from "./Modal";

interface Props {
  memberName: string;
  onClose: () => void;
  onSubmit: (body: { amount: number; category: string; note: string }) => void;
  busy: boolean;
}

export function AddChargeModal({ memberName, onClose, onSubmit, busy }: Props) {
  const [amount, setAmount] = useState("");
  const [category, setCategory] = useState<string>(LEDGER_CHARGE_CATEGORIES[0]);
  const [note, setNote] = useState("");

  const parsed = Number.parseFloat(amount);
  const canSubmit = !busy && Number.isFinite(parsed) && parsed > 0;

  return (
    <Modal
      title={`Add charge — ${memberName}`}
      onClose={onClose}
      submitLabel={busy ? "Posting…" : "Post charge"}
      onSubmit={
        canSubmit
          ? () => onSubmit({ amount: parsed, category, note })
          : undefined
      }
    >
      <div className="field">
        <label>Amount</label>
        <input
          className="input"
          type="number"
          step="0.01"
          min="0.01"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          placeholder="0.00"
          autoFocus
        />
      </div>
      <div className="field">
        <label>Category</label>
        <select
          className="select"
          value={category}
          onChange={(e) => setCategory(e.target.value)}
        >
          {LEDGER_CHARGE_CATEGORIES.map((c) => (
            <option key={c} value={c}>
              {c}
            </option>
          ))}
        </select>
      </div>
      <div className="field">
        <label>Note</label>
        <input
          className="input"
          value={note}
          onChange={(e) => setNote(e.target.value)}
          placeholder="Optional"
        />
      </div>
    </Modal>
  );
}
