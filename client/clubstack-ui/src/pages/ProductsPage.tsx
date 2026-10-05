import { useEffect, useState, type FormEvent } from 'react';
import { describeError, json } from '../api/client';
import { useAuth } from '../auth/AuthContext';
import type { Product, ProductCategory, ProductPrice, ProductSearch, Supplier, SupplierSearch } from '../types';

interface ProductRow extends Product {
  currentPrice?: ProductPrice | null;
}

type ProductsTab = 'products' | 'categories';

export function ProductsPage() {
  const { can } = useAuth();
  const [tab, setTab] = useState<ProductsTab>('products');
  const [rows, setRows] = useState<ProductRow[]>([]);
  const [categories, setCategories] = useState<ProductCategory[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [error, setError] = useState('');
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    sku: '',
    name: '',
    categoryId: '',
    unitOfMeasure: 'Each',
    barcode: '',
    stockManaged: true,
    cost: '0',
    priceAmount: '',
    reorderLevel: '',
    supplierId: '',
    currencyCode: 'ZAR',
  });
  const [categoryForm, setCategoryForm] = useState({ code: '', name: '', description: '' });
  const [editingCategory, setEditingCategory] = useState<ProductCategory | null>(null);
  const [creatingCategory, setCreatingCategory] = useState(false);

  const canView = can('Inventory.Product.View');
  const canCreate = can('Inventory.Product.Create');
  const canEdit = can('Inventory.Product.Edit');

  const load = async () => {
    setError('');
    try {
      const search = await json.get<ProductSearch>('/api/v1/products?pageSize=100');
      const enriched = await Promise.all(
        search.items.map(async (product) => {
          try {
            const prices = await json.get<ProductPrice[]>(`/api/v1/products/${product.id}/prices`);
            return { ...product, currentPrice: prices.length > 0 ? prices[prices.length - 1] : null };
          } catch {
            return { ...product, currentPrice: null };
          }
        }),
      );
      setRows(enriched);
    } catch (err) {
      setError(describeError(err));
    }
  };

  const loadCategories = async () => {
    try {
      const cats = await json.get<ProductCategory[]>('/api/v1/product-categories?includeInactive=true');
      setCategories(cats);
    } catch (err) {
      setError(describeError(err));
    }
  };

  useEffect(() => {
    if (!canView) {
      return;
    }
    void load();
    void (async () => {
      try {
        const [cats, sups] = await Promise.all([
          json.get<ProductCategory[]>('/api/v1/product-categories'),
          json.get<SupplierSearch>('/api/v1/suppliers'),
        ]);
        setCategories(cats);
        setSuppliers(sups.items);
      } catch (err) {
        setError(describeError(err));
      }
    })();
  }, [canView]);

  const createProduct = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<Product>('/api/v1/products', {
        ...form,
        cost: Number(form.cost),
        priceAmount: form.priceAmount ? Number(form.priceAmount) : null,
        reorderLevel: form.reorderLevel ? Number(form.reorderLevel) : null,
        supplierId: form.supplierId || null,
        stockManaged: form.stockManaged,
      });
      setCreating(false);
      setForm({ ...form, sku: '', name: '', barcode: '', cost: '0', priceAmount: '', reorderLevel: '', supplierId: '' });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const setPrice = async (productId: string, amount: string) => {
    if (!amount) {
      return;
    }
    setBusy(true);
    try {
      await json.post<ProductPrice>(`/api/v1/products/${productId}/prices`, { amount: Number(amount) });
      await load();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const createCategory = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    try {
      await json.post<ProductCategory>('/api/v1/product-categories', {
        code: categoryForm.code,
        name: categoryForm.name,
        description: categoryForm.description,
      });
      setCreatingCategory(false);
      setCategoryForm({ code: '', name: '', description: '' });
      await loadCategories();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const updateCategory = async (event: FormEvent) => {
    event.preventDefault();
    if (!editingCategory) return;
    setBusy(true);
    try {
      await json.put<ProductCategory>(`/api/v1/product-categories/${editingCategory.id}`, {
        name: categoryForm.name,
        description: categoryForm.description,
        isActive: editingCategory.isActive,
      });
      setEditingCategory(null);
      setCategoryForm({ code: '', name: '', description: '' });
      await loadCategories();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  const toggleCategoryStatus = async (category: ProductCategory) => {
    setBusy(true);
    try {
      await json.put<ProductCategory>(`/api/v1/product-categories/${category.id}`, {
        name: category.name,
        description: category.description,
        isActive: !category.isActive,
      });
      await loadCategories();
    } catch (err) {
      setError(describeError(err));
    } finally {
      setBusy(false);
    }
  };

  if (!canView) {
    return <div className="empty-state">You do not have permission to view products.</div>;
  }

  return (
    <>
      <h1 className="page-title">Products & Inventory</h1>
      <p className="page-subtitle">Product catalogue, categories, and inventory management.</p>

      {error ? <div className="alert alert-error">{error}</div> : null}

      <div className="tab-bar">
        <button
          className={`tab ${tab === 'products' ? 'active' : ''}`}
          onClick={() => setTab('products')}
        >
          Products
        </button>
        <button
          className={`tab ${tab === 'categories' ? 'active' : ''}`}
          onClick={() => setTab('categories')}
        >
          Categories
        </button>
      </div>

      {tab === 'products' && (
        <>
          <div className="toolbar">
            {canCreate ? (
              <button className="btn btn-primary" onClick={() => setCreating((value) => !value)}>
                {creating ? 'Close' : 'New product'}
              </button>
            ) : null}
          </div>

          {creating ? (
            <div className="card">
              <h3>New product</h3>
              <form className="form-grid" onSubmit={(event) => void createProduct(event)}>
                <div className="field">
                  <label htmlFor="sku">SKU *</label>
                  <input id="sku" required maxLength={32} value={form.sku} onChange={(event) => setForm({ ...form, sku: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="name">Name *</label>
                  <input id="name" required maxLength={160} value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="categoryId">Category *</label>
                  <select id="categoryId" required value={form.categoryId} onChange={(event) => setForm({ ...form, categoryId: event.target.value })}>
                    <option value="">Select…</option>
                    {categories.filter((c) => c.isActive).map((c) => (
                      <option key={c.id} value={c.id}>{c.name}</option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label htmlFor="uom">Unit of measure *</label>
                  <input id="uom" required maxLength={24} value={form.unitOfMeasure} onChange={(event) => setForm({ ...form, unitOfMeasure: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="barcode">Barcode</label>
                  <input id="barcode" maxLength={64} value={form.barcode} onChange={(event) => setForm({ ...form, barcode: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="cost">Cost *</label>
                  <input id="cost" type="number" min={0} step={0.01} required value={form.cost} onChange={(event) => setForm({ ...form, cost: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="price">Selling price</label>
                  <input id="price" type="number" min={0.01} step={0.01} value={form.priceAmount} onChange={(event) => setForm({ ...form, priceAmount: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="reorder">Reorder level</label>
                  <input id="reorder" type="number" min={0} step={1} value={form.reorderLevel} onChange={(event) => setForm({ ...form, reorderLevel: event.target.value })} />
                </div>
                <div className="field">
                  <label htmlFor="supplierId">Preferred supplier</label>
                  <select id="supplierId" value={form.supplierId} onChange={(event) => setForm({ ...form, supplierId: event.target.value })}>
                    <option value="">None</option>
                    {suppliers.filter((s) => s.status === 'Active').map((s) => (
                      <option key={s.id} value={s.id}>{s.name}</option>
                    ))}
                  </select>
                </div>
                <div className="field">
                  <label htmlFor="currency">Currency</label>
                  <input id="currency" maxLength={8} value={form.currencyCode} onChange={(event) => setForm({ ...form, currencyCode: event.target.value })} />
                </div>
                <label className="field" style={{ flexDirection: 'row', gap: 6, alignItems: 'center' }}>
                  <input type="checkbox" checked={form.stockManaged} onChange={(event) => setForm({ ...form, stockManaged: event.target.checked })} />
                  Track stock (stock-managed product)
                </label>
                <button type="submit" className="btn btn-primary" disabled={busy}>
                  Create product
                </button>
              </form>
            </div>
          ) : null}

          {rows.length === 0 ? (
            <div className="empty-state">No products yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>SKU</th>
                  <th>Name</th>
                  <th>Category</th>
                  <th>UoM</th>
                  <th>Cost</th>
                  <th>Selling price</th>
                  <th>Status</th>
                  <th>{canEdit ? 'Actions' : ''}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((product) => (
                  <tr key={product.id}>
                    <td>{product.sku}</td>
                    <td>{product.name}</td>
                    <td>{product.categoryName || '—'}</td>
                    <td>{product.unitOfMeasure}</td>
                    <td>{product.cost.toLocaleString(undefined, { minimumFractionDigits: 2 })} {product.currencyCode}</td>
                    <td>
                      {product.currentPrice
                        ? `${product.currentPrice.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })} ${product.currentPrice.currencyCode}`
                        : 'No price'}
                    </td>
                    <td><span className={`status-pill ${product.status === 'Active' ? 'good' : 'bad'}`}>{product.status}</span></td>
                    <td>
                      {canEdit ? (
                        <form style={{ display: 'flex', gap: 6 }} onSubmit={(e) => { e.preventDefault(); void setPrice(product.id, (e.currentTarget[0] as HTMLInputElement).value); }}>
                          <input type="number" min={0.01} step={0.01} placeholder="New price" style={{ width: 110 }} />
                          <button type="submit" className="btn btn-secondary">Price</button>
                        </form>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}

      {tab === 'categories' && (
        <>
          <div className="toolbar">
            {canCreate ? (
              <button
                className="btn btn-primary"
                onClick={() => {
                  setCreatingCategory(true);
                  setEditingCategory(null);
                  setCategoryForm({ code: '', name: '', description: '' });
                }}
              >
                New category
              </button>
            ) : null}
          </div>

          {creatingCategory ? (
            <div className="card">
              <h3>New category</h3>
              <form className="form-grid" onSubmit={(event) => void createCategory(event)}>
                <div className="field">
                  <label htmlFor="catCode">Code *</label>
                  <input
                    id="catCode"
                    required
                    maxLength={16}
                    value={categoryForm.code}
                    onChange={(event) => setCategoryForm({ ...categoryForm, code: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="catName">Name *</label>
                  <input
                    id="catName"
                    required
                    maxLength={160}
                    value={categoryForm.name}
                    onChange={(event) => setCategoryForm({ ...categoryForm, name: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="catDesc">Description</label>
                  <textarea
                    id="catDesc"
                    maxLength={512}
                    value={categoryForm.description}
                    onChange={(event) => setCategoryForm({ ...categoryForm, description: event.target.value })}
                  />
                </div>
                <button type="submit" className="btn btn-primary" disabled={busy}>
                  Create category
                </button>
                <button type="button" className="btn" onClick={() => setCreatingCategory(false)}>
                  Cancel
                </button>
              </form>
            </div>
          ) : null}

          {editingCategory ? (
            <div className="card">
              <h3>Edit category</h3>
              <form className="form-grid" onSubmit={(event) => void updateCategory(event)}>
                <div className="field">
                  <label>Code</label>
                  <input value={editingCategory.code} disabled />
                </div>
                <div className="field">
                  <label htmlFor="editCatName">Name *</label>
                  <input
                    id="editCatName"
                    required
                    maxLength={160}
                    value={categoryForm.name}
                    onChange={(event) => setCategoryForm({ ...categoryForm, name: event.target.value })}
                  />
                </div>
                <div className="field">
                  <label htmlFor="editCatDesc">Description</label>
                  <textarea
                    id="editCatDesc"
                    maxLength={512}
                    value={categoryForm.description}
                    onChange={(event) => setCategoryForm({ ...categoryForm, description: event.target.value })}
                  />
                </div>
                <button type="submit" className="btn btn-primary" disabled={busy}>
                  Save
                </button>
                <button type="button" className="btn" onClick={() => setEditingCategory(null)}>
                  Cancel
                </button>
              </form>
            </div>
          ) : null}

          {categories.length === 0 ? (
            <div className="empty-state">No categories configured yet.</div>
          ) : (
            <table>
              <thead>
                <tr>
                  <th>Code</th>
                  <th>Name</th>
                  <th>Description</th>
                  <th>Status</th>
                  <th>{canEdit ? 'Actions' : ''}</th>
                </tr>
              </thead>
              <tbody>
                {categories.map((category) => (
                  <tr key={category.id}>
                    <td>{category.code}</td>
                    <td>{category.name}</td>
                    <td>{category.description || '—'}</td>
                    <td>
                      <span className={`status-pill ${category.isActive ? 'good' : 'bad'}`}>
                        {category.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                    <td>
                      {canEdit ? (
                        <div style={{ display: 'flex', gap: 6 }}>
                          <button
                            className="btn btn-secondary"
                            onClick={() => {
                              setEditingCategory(category);
                              setCreatingCategory(false);
                              setCategoryForm({ code: category.code, name: category.name, description: category.description ?? '' });
                            }}
                          >
                            Edit
                          </button>
                          <button
                            className="btn"
                            onClick={() => void toggleCategoryStatus(category)}
                          >
                            {category.isActive ? 'Deactivate' : 'Activate'}
                          </button>
                        </div>
                      ) : null}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </>
      )}
    </>
  );
}
