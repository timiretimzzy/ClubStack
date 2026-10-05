# QA Report: Product Category Creation Blocker

**Date:** 2026-09-28
**Reported by:** QA Session
**Severity:** P3 — Product Defect
**Status:** BLOCKS PRODUCT TESTING

---

## Issue Summary

Product categories cannot be added through the UI. This blocks:
- Creating products (products require a category)
- Testing any workflow that requires products (POS sales, stock management, etc.)

---

## Root Cause

The **ProductsPage** (`/products`) loads product categories from `GET /api/v1/product-categories` but provides **no UI to create new categories**. The API endpoint `POST /api/v1/product-categories` exists and works, but there is no "New category" button or category management interface.

### Evidence

1. **ProductsPage.tsx** — has a "New product" button but no "New category" button
2. **Layout.tsx** — no "Product Categories" navigation item
3. **ProductCategoriesController.cs** — API endpoint exists and is functional
4. **No ProductCategoriesPage** — no dedicated page for managing product categories

---

## Impact

| Blocked Workflow | Reason |
|---|---|
| Create product | `categoryId` is required but no categories exist |
| POS sale | Requires products to sell |
| Stock management | Requires products to track |
| Product pricing | Requires products to price |
| Supplier linking | Requires products to link |

---

## Tab Grouping Suggestion

The current flat navigation has 20+ items. Consider grouping into collapsible categories:

| Group | Tabs |
|---|---|
| **Membership** | Members, Applications, Categories, Households, Fee Assessments |
| **Inventory** | Products, Stock, Transfers, Counts, Adjustments, Wastage, Reorder Alerts, Suppliers |
| **POS** | Point of Sale, Sales, Cashier Sessions |
| **Finance** | Finance Overview, Chart of Accounts, Journals, Receivables, Payables, Payments, Periods, Reports, Reconciliation |
| **Golf** | Golf Courses, Tee Sheet, Bookings, Guests, Charges |
| **Admin** | Fiscalisation |

---

## Recommended Fix

### Option A: Add category management to ProductsPage
- Add a "New category" button next to "New product"
- Add a category creation form (code, name, description)
- Add a category list/table below the product list

### Option B: Create a dedicated ProductCategoriesPage
- New page at `/product-categories`
- Full CRUD for product categories
- Add navigation item under Inventory group

### Option C: Inline category selector with quick-add
- In the product form, replace the category dropdown with a combobox
- Allow quick-creation of new categories inline

---

## Additional Findings

1. **Membership categories** (`/categories`) DO have a "New category" button — this works correctly
2. **Product categories** are a separate concept from membership categories
3. The API supports product category creation — only the UI is missing

---

## Next Steps

1. **Fix:** Add product category creation UI (Option A or B)
2. **Verify:** Create a category, then create a product with that category
3. **Test:** Run full product workflow (create → price → stock → POS sale)
4. **Improve:** Implement tab grouping for better navigation

---

## QA Recorder Evidence

Session ID: QA-SESSION-...
- Clicked "New product" → form requires categoryId → dropdown empty → BLOCKED
- No "New category" button found on ProductsPage
- No "Product Categories" navigation item found
