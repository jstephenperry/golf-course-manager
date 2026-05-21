import { useState } from "react";
import { LEDGER_METHODS } from "../data/types";
import { Modal } from "./Modal";

interface Props {
  memberName: string;
  currentBalance: number;
  onClose: () => void;
  onSubmit: (body: { amount: number; method: string; note: string }) => void;
  busy: boolean;
}

export function TakePaymentModal({
  memberName,
  currentBalance,
  onClose,
  onSubmit,
  busy,
}: Props) {
  const [amount, setAmount] = useState(
    currentBalance > 0 ? currentBalance.toFixed(2) : "",
  );
  const [method, setMethod] = useState<string>(LEDGER_METHODS[0]);
  const [note, setNote] = useState("");

  const parsed = Number.parseFloat(amount);
  const canSubmit = !busy && Number.isFinite(parsed) && parsed > 0;

  return (
    <Modal
      title={`Take payment — ${memberName}`}
      onClose={onClose}
      submitLabel={busy ? "Posting…" : "Record payment"}
      onSubmit={
        canSubmit
          ? () => onSubmit({ amount: parsed, method, note })
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
        {currentBalance > 0 && (
          <div className="muted" style={{ fontSize: 12, marginTop: 4 }}>
            Pre-filled with current balance ${currentBalance.toFixed(2)}.
          </div>
        )}
      </div>
      <div className="field">
        <label>Method</label>
        <select
          className="select"
          value={method}
          onChange={(e) => setMethod(e.target.value)}
        >
          {LEDGER_METHODS.map((m) => (
            <option key={m} value={m}>
              {m}
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
