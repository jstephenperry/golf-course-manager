import { useMemo, useState } from "react";
import {
  PRODUCTS_STOCK,
  PRODUCTS_WRITE,
} from "../auth/permissions";
import { RequirePermission } from "../auth/RequirePermission";
import { Modal } from "../components/Modal";
import { useToaster } from "../components/Toaster";
import { useStore } from "../data/store";
import type { Product, ProductCategory } from "../data/types";
import { formatCount, formatMoney } from "../data/utils";

const CATEGORIES: ProductCategory[] = [
  "Clubs",
  "Balls",
  "Apparel",
  "Accessories",
  "Food & Beverage",
];

const blank = (): Omit<Product, "id"> => ({
  name: "",
  category: "Accessories",
  sku: "",
  price: 0,
  cost: 0,
  stock: 0,
  reorderLevel: 5,
});

export function ProShop() {
  const { data, products: api } = useStore();
  const toaster = useToaster();
  const [busy, setBusy] = useState(false);
  const [category, setCategory] = useState<string>("all");
  const [search, setSearch] = useState("");
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Product | null>(null);
  const [form, setForm] = useState<Omit<Product, "id">>(blank());

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return data.products.filter((p) => {
      if (category !== "all" && p.category !== category) return false;
      if (!q) return true;
      return (
        p.name.toLowerCase().includes(q) || p.sku.toLowerCase().includes(q)
      );
    });
  }, [data.products, category, search]);

  const totals = useMemo(() => {
    const inventoryValue = data.products.reduce(
      (sum, p) => sum + p.cost * p.stock,
      0,
    );
    const retailValue = data.products.reduce(
      (sum, p) => sum + p.price * p.stock,
      0,
    );
    const lowStock = data.products.filter(
      (p) => p.stock <= p.reorderLevel,
    ).length;
    return { inventoryValue, retailValue, lowStock };
  }, [data.products]);

  const save = async () => {
    if (!form.name.trim()) {
      toaster.push({ kind: "error", message: "Name is required." });
      return;
    }
    setBusy(true);
    const result = editing
      ? await api.update(editing.id, { ...editing, ...form })
      : await api.create(form);
    setBusy(false);
    if (!result) return;
    setEditing(null);
    setCreating(false);
  };

  const remove = async (id: string) => {
    if (!window.confirm("Delete this product?")) return;
    setBusy(true);
    const ok = await api.remove(id);
    setBusy(false);
    if (ok) setEditing(null);
  };

  const adjustStock = async (id: string, delta: number) => {
    await api.adjustStock(id, delta);
  };

  return (
    <div className="stack">
      <div className="grid cols-3">
        <div className="kpi accent">
          <span className="label">Inventory Value (cost)</span>
          <span className="value">{formatMoney(totals.inventoryValue)}</span>
        </div>
        <div className="kpi">
          <span className="label">Retail Value</span>
          <span className="value">{formatMoney(totals.retailValue)}</span>
        </div>
        <div className="kpi">
          <span className="label">Items At/Below Reorder</span>
          <span className="value">{formatCount(totals.lowStock)}</span>
        </div>
      </div>

      <div className="card">
        <div className="toolbar">
          <div className="toolbar-left">
            <input
              className="input"
              placeholder="Search products"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{ minWidth: 220 }}
            />
            <select
              className="select"
              value={category}
              onChange={(e) => setCategory(e.target.value)}
            >
              <option value="all">All categories</option>
              {CATEGORIES.map((c) => (
                <option key={c}>{c}</option>
              ))}
            </select>
          </div>
          <RequirePermission permission={PRODUCTS_WRITE}>
            <button
              className="btn"
              onClick={() => {
                setForm(blank());
                setCreating(true);
              }}
            >
              + Add Product
            </button>
          </RequirePermission>
        </div>

        {filtered.length === 0 ? (
          <div className="empty">No products match.</div>
        ) : (
          <table className="table">
            <thead>
              <tr>
                <th>Product</th>
                <th>SKU</th>
                <th>Category</th>
                <th>Price</th>
                <th>Cost</th>
                <th>Stock</th>
                <th>Reorder</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((p) => (
                <tr key={p.id}>
                  <td>
                    <strong>{p.name}</strong>
                  </td>
                  <td>
                    <span className="muted">{p.sku}</span>
                  </td>
                  <td>
                    <span className="pill">{p.category}</span>
                  </td>
                  <td>{formatMoney(p.price)}</td>
                  <td>{formatMoney(p.cost)}</td>
                  <td>
                    <div className="row" style={{ gap: 6 }}>
                      <RequirePermission permission={PRODUCTS_STOCK}>
                        <button
                          className="btn sm secondary"
                          onClick={() => adjustStock(p.id, -1)}
                        >
                          −
                        </button>
                      </RequirePermission>
                      <span
                        className={`pill ${
                          p.stock === 0
                            ? "red"
                            : p.stock <= p.reorderLevel
                              ? "gold"
                              : "green"
                        }`}
                      >
                        {formatCount(p.stock)}
                      </span>
                      <RequirePermission permission={PRODUCTS_STOCK}>
                        <button
                          className="btn sm secondary"
                          onClick={() => adjustStock(p.id, 1)}
                        >
                          +
                        </button>
                      </RequirePermission>
                    </div>
                  </td>
                  <td>{formatCount(p.reorderLevel)}</td>
                  <td>
                    <div className="table-actions">
                      <RequirePermission permission={PRODUCTS_WRITE}>
                        <button
                          className="btn sm secondary"
                          onClick={() => {
                            setEditing(p);
                            setForm({ ...p });
                          }}
                        >
                          Edit
                        </button>
                      </RequirePermission>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {(creating || editing) && (
        <Modal
          title={editing ? "Edit Product" : "Add Product"}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSubmit={save}
          submitLabel={busy ? "Saving…" : editing ? "Save" : "Add"}
        >
          <div className="field">
            <label>Name</label>
            <input
              className="input"
              value={form.name}
              onChange={(e) => setForm({ ...form, name: e.target.value })}
            />
          </div>
          <div className="grid cols-2">
            <div className="field">
              <label>SKU</label>
              <input
                className="input"
                value={form.sku}
                onChange={(e) => setForm({ ...form, sku: e.target.value })}
              />
            </div>
            <div className="field">
              <label>Category</label>
              <select
                className="select"
                value={form.category}
                onChange={(e) =>
                  setForm({
                    ...form,
                    category: e.target.value as ProductCategory,
                  })
                }
              >
                {CATEGORIES.map((c) => (
                  <option key={c}>{c}</option>
                ))}
              </select>
            </div>
          </div>
          <div className="grid cols-4">
            <div className="field">
              <label>Price ($)</label>
              <input
                className="input"
                type="number"
                step="0.01"
                value={form.price}
                onChange={(e) =>
                  setForm({ ...form, price: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Cost ($)</label>
              <input
                className="input"
                type="number"
                step="0.01"
                value={form.cost}
                onChange={(e) =>
                  setForm({ ...form, cost: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Stock</label>
              <input
                className="input"
                type="number"
                value={form.stock}
                onChange={(e) =>
                  setForm({ ...form, stock: Number(e.target.value) })
                }
              />
            </div>
            <div className="field">
              <label>Reorder at</label>
              <input
                className="input"
                type="number"
                value={form.reorderLevel}
                onChange={(e) =>
                  setForm({ ...form, reorderLevel: Number(e.target.value) })
                }
              />
            </div>
          </div>
          {editing && (
            <div style={{ textAlign: "right" }}>
              <button
                className="btn sm danger"
                onClick={() => remove(editing.id)}
              >
                Delete product
              </button>
            </div>
          )}
        </Modal>
      )}
    </div>
  );
}
