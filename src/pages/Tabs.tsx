import { useMemo, useState, type ReactNode } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Modal } from "../components/Modal";
import { uid, useStore } from "../data/store";
import type {
  PaymentMethod,
  PlayerTab,
  Product,
  TabLineItem,
  TabPayment,
} from "../data/types";
import {
  formatDateTime,
  formatMoney,
  tabTotals,
} from "../data/utils";

const PAYMENT_METHODS: PaymentMethod[] = [
  "Cash",
  "Card",
  "Member Charge",
  "Comp",
];

const DEFAULT_TAX_RATE = 0.0825;

const blankTab = (): Omit<PlayerTab, "id"> => ({
  openedAt: new Date().toISOString(),
  status: "Open",
  memberIds: [],
  guests: [],
  items: [],
  payments: [],
  tipAmount: 0,
  taxRate: DEFAULT_TAX_RATE,
  notes: "",
});

type Filter = "open" | "settled" | "all";

export function Tabs() {
  const { data, update } = useStore();
  const [params, setParams] = useSearchParams();
  const openId = params.get("tab");
  const [filter, setFilter] = useState<Filter>("open");
  const [creating, setCreating] = useState(false);
  const [newForm, setNewForm] = useState<Omit<PlayerTab, "id">>(blankTab());
  const [guestDraft, setGuestDraft] = useState("");

  const activeTab = data.tabs.find((t) => t.id === openId) ?? null;

  const list = useMemo(() => {
    return data.tabs
      .filter((t) => {
        if (filter === "open") return t.status === "Open";
        if (filter === "settled") return t.status === "Settled";
        return true;
      })
      .sort((a, b) => b.openedAt.localeCompare(a.openedAt));
  }, [data.tabs, filter]);

  const memberName = (id: string) => {
    const m = data.members.find((x) => x.id === id);
    return m ? `${m.firstName} ${m.lastName}` : id;
  };

  const tabLabel = (tab: PlayerTab) => {
    const names = [...tab.memberIds.map(memberName), ...tab.guests];
    return names.length > 0 ? names.join(", ") : "(no players)";
  };

  const openTabView = (id: string) => setParams({ tab: id });
  const closeTabView = () => setParams({});

  const createTab = () => {
    if (newForm.memberIds.length === 0 && newForm.guests.length === 0) {
      alert("Add at least one member or guest.");
      return;
    }
    const id = uid("tab");
    const tab: PlayerTab = { id, ...newForm };
    update("tabs", (list) => [tab, ...list]);
    setCreating(false);
    setNewForm(blankTab());
    setGuestDraft("");
    openTabView(id);
  };

  const updateTab = (id: string, patch: Partial<PlayerTab>) => {
    update("tabs", (list) =>
      list.map((t) => (t.id === id ? { ...t, ...patch } : t)),
    );
  };

  const adjustStock = (productId: string, delta: number) => {
    update("products", (products) =>
      products.map((p) =>
        p.id === productId
          ? { ...p, stock: Math.max(0, p.stock + delta) }
          : p,
      ),
    );
  };

  const addItem = (
    tab: PlayerTab,
    product: Product,
    qty = 1,
    notes = "",
  ) => {
    if (product.stock < qty) {
      if (
        !window.confirm(
          `Only ${product.stock} of ${product.name} in stock. Add anyway?`,
        )
      ) {
        return;
      }
    }
    const lineItem: TabLineItem = {
      id: uid("li"),
      productId: product.id,
      name: product.name,
      unitPrice: product.price,
      quantity: qty,
      notes,
      addedAt: new Date().toISOString(),
    };
    adjustStock(product.id, -qty);
    updateTab(tab.id, { items: [...tab.items, lineItem] });
  };

  const removeItem = (tab: PlayerTab, item: TabLineItem) => {
    if (!window.confirm(`Remove ${item.name}?`)) return;
    adjustStock(item.productId, item.quantity);
    updateTab(tab.id, { items: tab.items.filter((i) => i.id !== item.id) });
  };

  const changeItemQty = (tab: PlayerTab, item: TabLineItem, delta: number) => {
    const next = item.quantity + delta;
    if (next < 1) return;
    adjustStock(item.productId, -delta);
    updateTab(tab.id, {
      items: tab.items.map((i) =>
        i.id === item.id ? { ...i, quantity: next } : i,
      ),
    });
  };

  const addPayment = (tab: PlayerTab, payment: Omit<TabPayment, "id">) => {
    const p: TabPayment = { id: uid("pay"), ...payment };
    if (
      p.method === "Member Charge" &&
      p.payerMemberId &&
      p.amount > 0
    ) {
      update("members", (list) =>
        list.map((m) =>
          m.id === p.payerMemberId ? { ...m, balance: m.balance + p.amount } : m,
        ),
      );
    }
    updateTab(tab.id, { payments: [...tab.payments, p] });
  };

  const removePayment = (tab: PlayerTab, payment: TabPayment) => {
    if (!window.confirm("Remove this payment?")) return;
    if (
      payment.method === "Member Charge" &&
      payment.payerMemberId &&
      payment.amount > 0
    ) {
      update("members", (list) =>
        list.map((m) =>
          m.id === payment.payerMemberId
            ? { ...m, balance: Math.max(0, m.balance - payment.amount) }
            : m,
        ),
      );
    }
    updateTab(tab.id, {
      payments: tab.payments.filter((p) => p.id !== payment.id),
    });
  };

  const settleTab = (tab: PlayerTab) => {
    const totals = tabTotals(tab);
    if (totals.balance > 0.005) {
      alert(
        `Balance ${formatMoney(totals.balance)} still owed. Apply payment first.`,
      );
      return;
    }
    updateTab(tab.id, {
      status: "Settled",
      closedAt: new Date().toISOString(),
    });
  };

  const reopenTab = (tab: PlayerTab) => {
    updateTab(tab.id, { status: "Open", closedAt: undefined });
  };

  const voidTab = (tab: PlayerTab) => {
    if (
      !window.confirm(
        "Void this tab? All items will be returned to inventory and any Member Charges reversed.",
      )
    )
      return;
    for (const item of tab.items) {
      adjustStock(item.productId, item.quantity);
    }
    for (const p of tab.payments) {
      if (
        p.method === "Member Charge" &&
        p.payerMemberId &&
        p.amount > 0
      ) {
        update("members", (list) =>
          list.map((m) =>
            m.id === p.payerMemberId
              ? { ...m, balance: Math.max(0, m.balance - p.amount) }
              : m,
          ),
        );
      }
    }
    updateTab(tab.id, {
      status: "Voided",
      closedAt: new Date().toISOString(),
    });
  };

  const counts = {
    open: data.tabs.filter((t) => t.status === "Open").length,
    settled: data.tabs.filter((t) => t.status === "Settled").length,
    all: data.tabs.length,
  };

  return (
    <div className="stack">
      <div className="toolbar">
        <div className="toolbar-left">
          {(["open", "settled", "all"] as Filter[]).map((f) => (
            <button
              key={f}
              className={`btn ${filter === f ? "" : "secondary"}`}
              onClick={() => setFilter(f)}
            >
              {f === "open"
                ? `Open (${counts.open})`
                : f === "settled"
                  ? `Settled (${counts.settled})`
                  : `All (${counts.all})`}
            </button>
          ))}
        </div>
        <button
          className="btn"
          onClick={() => {
            setNewForm(blankTab());
            setGuestDraft("");
            setCreating(true);
          }}
        >
          + Open Tab
        </button>
      </div>

      {list.length === 0 ? (
        <div className="card empty">
          {filter === "open"
            ? "No open tabs. Open one from here, or use the Tab button on a tee time row."
            : "Nothing here."}
        </div>
      ) : (
        <div className="grid cols-2">
          {list.map((t) => {
            const totals = tabTotals(t);
            return (
              <div className="card" key={t.id}>
                <div className="row between">
                  <div>
                    <strong>{tabLabel(t)}</strong>
                    <div className="muted" style={{ fontSize: 12 }}>
                      Opened {formatDateTime(t.openedAt)}
                      {t.teeTimeId && (
                        <>
                          {" · "}
                          <Link to="/tee-times">tee time</Link>
                        </>
                      )}
                    </div>
                  </div>
                  <span
                    className={`pill ${
                      t.status === "Open"
                        ? "gold"
                        : t.status === "Settled"
                          ? "green"
                          : "red"
                    }`}
                  >
                    {t.status}
                  </span>
                </div>
                <div className="row between" style={{ marginTop: 10 }}>
                  <span className="muted">
                    {t.items.length} item{t.items.length === 1 ? "" : "s"}
                  </span>
                  <div className="row">
                    <span className="muted" style={{ fontSize: 12 }}>
                      Total {formatMoney(totals.total)}
                    </span>
                    {totals.balance > 0.005 ? (
                      <span className="pill red">
                        Balance {formatMoney(totals.balance)}
                      </span>
                    ) : (
                      <span className="pill green">Paid</span>
                    )}
                  </div>
                </div>
                <div className="row" style={{ marginTop: 12 }}>
                  <button
                    className="btn sm"
                    onClick={() => openTabView(t.id)}
                  >
                    Open
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {activeTab && (
        <TabDetail
          tab={activeTab}
          onClose={closeTabView}
          memberName={memberName}
          allMembers={data.members}
          allProducts={data.products}
          addItem={(product, qty, notes) =>
            addItem(activeTab, product, qty, notes)
          }
          removeItem={(item) => removeItem(activeTab, item)}
          changeItemQty={(item, delta) => changeItemQty(activeTab, item, delta)}
          addPayment={(p) => addPayment(activeTab, p)}
          removePayment={(p) => removePayment(activeTab, p)}
          settle={() => settleTab(activeTab)}
          reopen={() => reopenTab(activeTab)}
          voidTab={() => voidTab(activeTab)}
          updateMeta={(patch) => updateTab(activeTab.id, patch)}
        />
      )}

      {creating && (
        <Modal
          title="Open New Tab"
          onClose={() => setCreating(false)}
          onSubmit={createTab}
          submitLabel="Open tab"
        >
          <div className="field">
            <label>Members on tab</label>
            <select
              className="select"
              multiple
              size={4}
              value={newForm.memberIds}
              onChange={(e) =>
                setNewForm({
                  ...newForm,
                  memberIds: Array.from(e.target.selectedOptions).map(
                    (o) => o.value,
                  ),
                })
              }
            >
              {data.members
                .filter((m) => m.active)
                .map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.firstName} {m.lastName} · {m.tier}
                  </option>
                ))}
            </select>
            <small className="muted">Hold Ctrl/Cmd to select multiple.</small>
          </div>
          <div className="field">
            <label>Guests (names)</label>
            <div className="row">
              <input
                className="input"
                placeholder="Add guest name"
                value={guestDraft}
                onChange={(e) => setGuestDraft(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    if (guestDraft.trim()) {
                      setNewForm({
                        ...newForm,
                        guests: [...newForm.guests, guestDraft.trim()],
                      });
                      setGuestDraft("");
                    }
                  }
                }}
              />
              <button
                type="button"
                className="btn sm secondary"
                onClick={() => {
                  if (guestDraft.trim()) {
                    setNewForm({
                      ...newForm,
                      guests: [...newForm.guests, guestDraft.trim()],
                    });
                    setGuestDraft("");
                  }
                }}
              >
                Add
              </button>
            </div>
            {newForm.guests.length > 0 && (
              <div className="chip-list" style={{ marginTop: 6 }}>
                {newForm.guests.map((g, i) => (
                  <span key={`${g}-${i}`} className="pill">
                    {g}{" "}
                    <button
                      type="button"
                      className="btn ghost sm"
                      style={{ padding: 0, marginLeft: 4 }}
                      onClick={() =>
                        setNewForm({
                          ...newForm,
                          guests: newForm.guests.filter((_, idx) => idx !== i),
                        })
                      }
                    >
                      ✕
                    </button>
                  </span>
                ))}
              </div>
            )}
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>Tax rate (%)</label>
              <input
                className="input"
                type="number"
                step="0.01"
                value={(newForm.taxRate * 100).toFixed(2)}
                onChange={(e) =>
                  setNewForm({
                    ...newForm,
                    taxRate: Number(e.target.value) / 100,
                  })
                }
              />
            </div>
            <div className="field">
              <label>Notes</label>
              <input
                className="input"
                value={newForm.notes}
                onChange={(e) =>
                  setNewForm({ ...newForm, notes: e.target.value })
                }
              />
            </div>
          </div>
        </Modal>
      )}
    </div>
  );
}

/* ---------------- Tab detail ---------------- */

interface TabDetailProps {
  tab: PlayerTab;
  onClose: () => void;
  memberName: (id: string) => string;
  allMembers: ReturnType<typeof useStore>["data"]["members"];
  allProducts: Product[];
  addItem: (product: Product, qty: number, notes: string) => void;
  removeItem: (item: TabLineItem) => void;
  changeItemQty: (item: TabLineItem, delta: number) => void;
  addPayment: (p: Omit<TabPayment, "id">) => void;
  removePayment: (p: TabPayment) => void;
  settle: () => void;
  reopen: () => void;
  voidTab: () => void;
  updateMeta: (patch: Partial<PlayerTab>) => void;
}

function TabDetail({
  tab,
  onClose,
  memberName,
  allMembers,
  allProducts,
  addItem,
  removeItem,
  changeItemQty,
  addPayment,
  removePayment,
  settle,
  reopen,
  voidTab,
  updateMeta,
}: TabDetailProps) {
  const totals = tabTotals(tab);
  const readonly = tab.status !== "Open";

  const [pickerProductId, setPickerProductId] = useState("");
  const [pickerQty, setPickerQty] = useState(1);
  const [pickerNotes, setPickerNotes] = useState("");

  const [payMethod, setPayMethod] = useState<PaymentMethod>("Card");
  const [payAmount, setPayAmount] = useState(totals.balance.toFixed(2));
  const [payer, setPayer] = useState<string>(tab.memberIds[0] ?? "");

  const product = allProducts.find((p) => p.id === pickerProductId) ?? null;

  const submitAdd = () => {
    if (!product) {
      alert("Pick a product.");
      return;
    }
    addItem(product, pickerQty, pickerNotes);
    setPickerProductId("");
    setPickerQty(1);
    setPickerNotes("");
  };

  const submitPayment = () => {
    const amount = Number(payAmount);
    if (!amount || amount <= 0) {
      alert("Enter a positive amount.");
      return;
    }
    if (payMethod === "Member Charge" && !payer) {
      alert("Pick which member to charge.");
      return;
    }
    addPayment({
      method: payMethod,
      amount,
      payerMemberId: payMethod === "Member Charge" ? payer : undefined,
      note: "",
      paidAt: new Date().toISOString(),
    });
    setPayAmount("0");
  };

  const applyBalance = () => setPayAmount(totals.balance.toFixed(2));

  return (
    <div className="modal-overlay" onMouseDown={onClose}>
      <div
        className="modal tab-modal"
        onMouseDown={(e) => e.stopPropagation()}
      >
        <div className="modal-header">
          <div>
            <h3 style={{ marginBottom: 4 }}>
              Tab ·{" "}
              {[...tab.memberIds.map(memberName), ...tab.guests].join(", ") ||
                "(no players)"}
            </h3>
            <div className="muted" style={{ fontSize: 12 }}>
              Opened {formatDateTime(tab.openedAt)}
              {tab.closedAt && ` · Closed ${formatDateTime(tab.closedAt)}`}
            </div>
          </div>
          <div className="row">
            <span
              className={`pill ${
                tab.status === "Open"
                  ? "gold"
                  : tab.status === "Settled"
                    ? "green"
                    : "red"
              }`}
            >
              {tab.status}
            </span>
            <button className="btn ghost" onClick={onClose} aria-label="Close">
              ✕
            </button>
          </div>
        </div>

        <div className="modal-body">
          <div className="grid cols-2" style={{ alignItems: "start" }}>
            {/* Items */}
            <div className="stack">
              <h4 style={{ margin: 0 }}>Items</h4>
              {tab.items.length === 0 ? (
                <div className="muted">No items on the tab yet.</div>
              ) : (
                <table className="table">
                  <thead>
                    <tr>
                      <th>Item</th>
                      <th style={{ width: 110, textAlign: "center" }}>Qty</th>
                      <th style={{ width: 90, textAlign: "right" }}>Price</th>
                      <th style={{ width: 90, textAlign: "right" }}>Line</th>
                      <th style={{ width: 1 }}></th>
                    </tr>
                  </thead>
                  <tbody>
                    {tab.items.map((li) => (
                      <tr key={li.id}>
                        <td>
                          <strong>{li.name}</strong>
                          {li.notes && (
                            <div className="muted" style={{ fontSize: 12 }}>
                              {li.notes}
                            </div>
                          )}
                        </td>
                        <td style={{ textAlign: "center" }}>
                          {readonly ? (
                            li.quantity
                          ) : (
                            <div
                              className="row"
                              style={{ justifyContent: "center", gap: 4 }}
                            >
                              <button
                                className="btn sm secondary"
                                onClick={() => changeItemQty(li, -1)}
                              >
                                −
                              </button>
                              <strong>{li.quantity}</strong>
                              <button
                                className="btn sm secondary"
                                onClick={() => changeItemQty(li, 1)}
                              >
                                +
                              </button>
                            </div>
                          )}
                        </td>
                        <td style={{ textAlign: "right" }}>
                          {formatMoney(li.unitPrice)}
                        </td>
                        <td style={{ textAlign: "right" }}>
                          {formatMoney(li.unitPrice * li.quantity)}
                        </td>
                        <td>
                          {!readonly && (
                            <button
                              className="btn sm danger"
                              onClick={() => removeItem(li)}
                            >
                              ✕
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {!readonly && (
                <div
                  className="card"
                  style={{ padding: 12, background: "var(--surface-2)" }}
                >
                  <strong style={{ fontSize: 13 }}>Add from inventory</strong>
                  <div className="field" style={{ marginTop: 8 }}>
                    <label>Product</label>
                    <select
                      className="select"
                      value={pickerProductId}
                      onChange={(e) => setPickerProductId(e.target.value)}
                    >
                      <option value="">— Select product —</option>
                      {allProducts.map((p) => (
                        <option
                          key={p.id}
                          value={p.id}
                          disabled={p.stock <= 0}
                        >
                          {p.name} · {formatMoney(p.price)} · stock {p.stock}
                          {p.stock <= 0 ? " (out)" : ""}
                        </option>
                      ))}
                    </select>
                    {allProducts.length === 0 && (
                      <small className="muted">
                        No products in inventory yet — add some in Pro Shop.
                      </small>
                    )}
                  </div>
                  <div className="grid cols-2" style={{ marginTop: 8 }}>
                    <div className="field">
                      <label>Qty</label>
                      <input
                        className="input"
                        type="number"
                        min={1}
                        value={pickerQty}
                        onChange={(e) =>
                          setPickerQty(Math.max(1, Number(e.target.value)))
                        }
                      />
                    </div>
                    <div className="field">
                      <label>Notes</label>
                      <input
                        className="input"
                        value={pickerNotes}
                        onChange={(e) => setPickerNotes(e.target.value)}
                      />
                    </div>
                  </div>
                  <div
                    className="row"
                    style={{ justifyContent: "flex-end", marginTop: 8 }}
                  >
                    <button className="btn" onClick={submitAdd}>
                      Add to tab
                    </button>
                  </div>
                </div>
              )}
            </div>

            {/* Totals & payments */}
            <div className="stack">
              <h4 style={{ margin: 0 }}>Totals</h4>
              <div className="card" style={{ padding: 12 }}>
                <Row label="Subtotal" value={formatMoney(totals.subtotal)} />
                <Row
                  label={`Tax (${(tab.taxRate * 100).toFixed(2)}%)`}
                  value={formatMoney(totals.tax)}
                />
                <div
                  className="row between"
                  style={{ alignItems: "center", padding: "6px 0" }}
                >
                  <span className="muted">Tip</span>
                  {readonly ? (
                    <span>{formatMoney(tab.tipAmount)}</span>
                  ) : (
                    <input
                      className="input"
                      type="number"
                      step="0.5"
                      style={{ width: 90, textAlign: "right" }}
                      value={tab.tipAmount}
                      onChange={(e) =>
                        updateMeta({ tipAmount: Number(e.target.value) || 0 })
                      }
                    />
                  )}
                </div>
                <hr
                  style={{
                    border: 0,
                    borderTop: "1px solid var(--border)",
                    margin: "4px 0",
                  }}
                />
                <Row
                  label={<strong>Total</strong>}
                  value={<strong>{formatMoney(totals.total)}</strong>}
                />
                <Row label="Paid" value={formatMoney(totals.paid)} />
                <Row
                  label={<strong>Balance</strong>}
                  value={
                    <strong
                      style={{
                        color:
                          totals.balance > 0.005
                            ? "var(--danger)"
                            : "var(--primary)",
                      }}
                    >
                      {formatMoney(totals.balance)}
                    </strong>
                  }
                />
              </div>

              <h4 style={{ margin: 0 }}>Payments</h4>
              {tab.payments.length === 0 ? (
                <div className="muted">No payments yet.</div>
              ) : (
                <table className="table">
                  <thead>
                    <tr>
                      <th>Method</th>
                      <th>Amount</th>
                      <th>When</th>
                      <th></th>
                    </tr>
                  </thead>
                  <tbody>
                    {tab.payments.map((p) => (
                      <tr key={p.id}>
                        <td>
                          <span className="pill">{p.method}</span>
                          {p.payerMemberId && (
                            <div className="muted" style={{ fontSize: 11 }}>
                              {memberName(p.payerMemberId)}
                            </div>
                          )}
                        </td>
                        <td>{formatMoney(p.amount)}</td>
                        <td>
                          <span className="muted" style={{ fontSize: 12 }}>
                            {formatDateTime(p.paidAt)}
                          </span>
                        </td>
                        <td>
                          {!readonly && (
                            <button
                              className="btn sm danger"
                              onClick={() => removePayment(p)}
                            >
                              ✕
                            </button>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}

              {!readonly && (
                <div
                  className="card"
                  style={{ padding: 12, background: "var(--surface-2)" }}
                >
                  <strong style={{ fontSize: 13 }}>Take payment</strong>
                  <div className="grid cols-2" style={{ marginTop: 8 }}>
                    <div className="field">
                      <label>Method</label>
                      <select
                        className="select"
                        value={payMethod}
                        onChange={(e) =>
                          setPayMethod(e.target.value as PaymentMethod)
                        }
                      >
                        {PAYMENT_METHODS.map((m) => (
                          <option key={m}>{m}</option>
                        ))}
                      </select>
                    </div>
                    <div className="field">
                      <label>Amount ($)</label>
                      <input
                        className="input"
                        type="number"
                        step="0.01"
                        value={payAmount}
                        onChange={(e) => setPayAmount(e.target.value)}
                      />
                    </div>
                  </div>
                  {payMethod === "Member Charge" && (
                    <div className="field" style={{ marginTop: 8 }}>
                      <label>Charge to member</label>
                      <select
                        className="select"
                        value={payer}
                        onChange={(e) => setPayer(e.target.value)}
                      >
                        <option value="">— Select —</option>
                        {(tab.memberIds.length > 0
                          ? allMembers.filter((m) =>
                              tab.memberIds.includes(m.id),
                            )
                          : allMembers
                        ).map((m) => (
                          <option key={m.id} value={m.id}>
                            {m.firstName} {m.lastName} (current{" "}
                            {formatMoney(m.balance)})
                          </option>
                        ))}
                      </select>
                    </div>
                  )}
                  <div
                    className="row between"
                    style={{ marginTop: 10, alignItems: "center" }}
                  >
                    <button
                      className="btn sm secondary"
                      onClick={applyBalance}
                      disabled={totals.balance <= 0}
                    >
                      Set to balance
                    </button>
                    <button className="btn" onClick={submitPayment}>
                      Apply payment
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        <div className="modal-footer">
          {tab.status === "Open" && (
            <>
              <button className="btn danger" onClick={voidTab}>
                Void tab
              </button>
              <div style={{ flex: 1 }} />
              <button className="btn secondary" onClick={onClose}>
                Close
              </button>
              <button className="btn" onClick={settle}>
                Settle &amp; close out
              </button>
            </>
          )}
          {tab.status === "Settled" && (
            <>
              <button className="btn secondary" onClick={reopen}>
                Reopen tab
              </button>
              <div style={{ flex: 1 }} />
              <button className="btn" onClick={onClose}>
                Done
              </button>
            </>
          )}
          {tab.status === "Voided" && (
            <>
              <div style={{ flex: 1 }} />
              <button className="btn" onClick={onClose}>
                Close
              </button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}

function Row({
  label,
  value,
}: {
  label: ReactNode;
  value: ReactNode;
}) {
  return (
    <div className="row between" style={{ padding: "4px 0" }}>
      <span className="muted">{label}</span>
      <span>{value}</span>
    </div>
  );
}
