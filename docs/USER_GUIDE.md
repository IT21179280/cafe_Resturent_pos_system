# CafePOS user guide

This guide is for cafe staff using the CafePOS desktop app. Menu names and permissions
depend on your role: Administrator, Manager, Cashier, or Kitchen.

## 1. Signing in

1. Start **CafePOS** from the Start menu or desktop shortcut.
2. Enter the server address provided by your administrator, for example
   `http://192.168.1.10:5080` or `http://localhost:5080`.
3. Enter your email and password and select **Sign in**.

If the administrator account is new, CafePOS asks you to set a new password before the main
window opens. Use at least 8 characters with uppercase, lowercase, a number, and a symbol.

The login screen can remember your email on this device. Your password is never stored.

## 2. Starting a shift

A shift must be open before payments can be taken.

1. Open **Shifts**.
2. Select **Open shift**.
3. Enter the opening cash float in the drawer.
4. Confirm.

During the shift:

- **Add expense** records cash paid out (for example vegetables or cleaning supplies).
- The expected cash figure updates from cash sales, refunds, and expenses.

At the end of the shift:

1. Count the cash drawer.
2. Select **Close shift** and enter the counted cash and any notes.
3. CafePOS shows the variance between expected and counted cash.
4. A manager/administrator can reopen a shift with an audit reason if a correction is
   required.

## 3. Taking an order in Point of Sale

1. Open **Point of Sale**.
2. Choose the order type:
   - **Dine-in** and select a table, or
   - **Takeaway**, or
   - **Delivery** and enter the customer and delivery details.
3. Search or browse products and select items.
4. For products with options, choose modifiers in the picker.
5. Adjust quantities and add item notes if needed.
6. Apply an item discount and/or an order discount if allowed.
7. Select **Send to kitchen** when the order is ready to prepare.
8. Select **Checkout** when it is time to pay.

Tips:

- Product stations decide whether the item appears on the kitchen or bar ticket.
- The cart total is recalculated by the server; the client does not send trusted totals.
- Service charge is applied from the **SERVICE CHARGE %** field below the cart; its amount is
  shown next to the field. The default percentage comes from Settings and can be changed for
  the current order.
- Tax is applied according to Settings and the product tax rate.

## 4. Payments and receipts

At checkout:

1. Choose **Cash**, **Card**, or **Bank transfer**. Cash and Card have dedicated options;
   enter a card approval code or transfer reference when needed.
2. Enter the amount tendered. Cash change is calculated automatically.
3. Add another payment line if the customer is splitting the bill.
4. Tick **Customer receipt** and/or **Kitchen ticket (KOT)** under **PRINT AFTER PAYMENT**.
5. Confirm the payment.

The checkout window shows the subtotal, discount, tax and service charge before payment.
CafePOS marks the order completed and deducts recipe stock according to the configured
inventory deduction event. Customer receipts and kitchen tickets can be printed to a Windows
printer or, if enabled, an ESC/POS thermal printer. The configured branding logo is printed at
the top of both the customer receipt and the kitchen ticket. Kitchen tickets list only stations,
quantities, modifiers and preparation notes, and are sent to the kitchen printer when one is
configured in Settings. Existing orders can be reprinted from **Orders**.

## 5. Tables

The **Tables** screen shows available, occupied, and reserved tables.

- **New table** adds a table and its area/capacity.
- **Open table in POS** opens the table's active order (or starts a new one).
- **View order** shows an existing order.
- **Mark reserved** holds a table for a guest.
- **Free table** releases a table when no active order remains.

Dine-in orders keep the table occupied until the order is completed or cancelled.

## 6. Kitchen display

The **Kitchen Display** screen updates automatically when orders are sent.

- Pending tickets are new orders.
- **Start** moves a ticket to preparing.
- **Ready** marks food ready to serve.
- **Served** removes the ticket from the active queue.

Use the station filter to show only Kitchen, Bar, Bakery, Grill, or Dessert items. Kitchen
users can only advance preparation status; they cannot take payments or change prices.

## 7. Orders

The **Orders** screen lists orders with filters for status, type, and date. Use the search
box for an order number or customer.

Actions depend on the order state and your role:

- **Mark completed** completes an order.
- **Print receipt** reprints the customer receipt.
- **Print kitchen ticket** reprints the kitchen ticket (KOT) for the selected order.
- **Cancel order** requires a reason. Only non-completed orders can be cancelled.
- **Record refund** creates a full or partial refund against a payment and requires a
  manager or administrator.
- **Previous / Next** page through results.

## 8. Menu and products

Administrators and managers manage the menu under **Menu & Products**.

- **New product** adds a product with category, SKU/barcode, price, tax rate, station, and
  availability.
- **Modifiers / extras** are added inside the product editor. Give each extra a name, an
  optional price, and an active status; staff can then select them in POS.
- **Recipe** links a product to ingredients and quantities for automatic stock deduction.
- **Edit selected** changes the product.
- **Delete selected** removes a product that is not in use.
- **Export CSV** creates a product spreadsheet.
- **Import CSV** creates or updates products from CSV. Review the result summary for
  skipped rows and errors.

Categories and modifiers are part of the product editor. Keep prices and tax rates current;
the server uses product data when pricing an order.

## 9. Inventory

The **Inventory** screen shows ingredients and current stock.

- **New** creates an ingredient with unit, cost, low-stock threshold, and optional default
  supplier.
- **Apply adjustment** adds or removes stock. A reason is required; decreases are recorded
  as wastage.
- Low-stock rows are highlighted and appear in the low-stock report.
- **Movements** shows the audit trail for purchases, sales, refunds, adjustments, and
  wastage.

Recipes control automatic deductions. If a product has no recipe, selling it does not
change ingredient stock.

## 10. Suppliers and purchasing

Under **Suppliers**:

- Create suppliers with contact and payment details.
- Deactivate or delete a supplier that is no longer used.

Under **Purchasing**:

1. Select **New purchase order**.
2. Choose the supplier, add ingredients, quantities, and unit costs.
3. Save and submit the order.
4. When goods arrive, open the purchase order and use **Receive**.
5. Enter the quantity received (partial receiving is supported).

Receiving adds stock and records a purchase receipt movement. The purchase order becomes
Partially received or Received automatically.

## 11. Reports

Open **Reports** and choose a date range and report:

- Sales
- Sales by cashier
- Sales by payment method
- Sales by category
- Best sellers
- Low stock
- Inventory movements
- Wastage
- Refunds
- Cancellations
- Shifts
- Gross profit

Select **Run report** to load the data, then **Export CSV** to save it. Reports use server
data, so they include orders from every till.

## 12. Users and roles

Administrators manage accounts under **Users**.

| Role | Typical access |
| --- | --- |
| Administrator | Everything, including users, settings, refunds, reopen shift, audit log |
| Manager | Menu, inventory, purchasing, reports, refunds, shift corrections |
| Cashier | POS, tables, orders, payments, shift open/close, own shift expenses |
| Kitchen | Kitchen display and preparation status only |

Create one account per person. Disable an account immediately when a staff member leaves.
Administrators can reset a password; the user should change it after signing in.

## 13. Settings

**Settings** has three sections:

- **POS branding, restaurant and Firebase** (administrator/manager): the system name and
  logo, restaurant name, address, phone, email, receipt footer, service charge, default tax,
  inventory deduction event, and the optional Firebase mirror.
  - The logo appears on the sign-in window, dashboard header, navigation sidebar, printed
    customer receipts and printed kitchen tickets. Use **Choose logo**, preview it, then
    **Save settings** to publish it. PNG, JPG, GIF, WEBP and BMP files up to 5 MB are accepted.
  - Product and category images are uploaded from **Menu & Products**. The POS product grid
    and category list show the uploaded image.
  - **Completed** deducts stock when the order is paid/completed (recommended).
  - **Served** deducts stock when food is marked served.
  - **Paid** deducts stock only on completed payment.
- **Firebase admin mirroring** (optional): enter the Firebase Realtime Database URL, enable
  sync, and add a database auth token only if your Firebase rules require one. Save, then
  press **Sync now** to test. The API sends a snapshot under the `cafepos` node every two
  minutes while enabled. The administrator can read that data in the Firebase console or an
  existing Firebase app.
- **Device** (this PC): server address, thermal printer name, use thermal printer, optional
  separate kitchen printer name and toggle, test connection, and change password. When no
  kitchen printer is configured, kitchen tickets use the receipt printer or the Windows
  print dialog.

Changing the server address on a client requires signing out and back in. The **Test
connection** button checks the API health endpoint.

### Connecting Firebase

1. Create a Firebase project and enable **Realtime Database**.
2. Copy the database URL, for example
   `https://your-project-default-rtdb.firebaseio.com`. Do not include `/cafepos.json`.
3. In CafePOS **Settings**, paste the URL under **Firebase admin mirroring** and tick
   **Enable Firebase sync**.
4. If the database rules require authentication, paste the database secret or token into
   **Database auth token**. The token is stored on the server and is never returned to the
   desktop app. Leave the field blank to keep an existing token. Do not append `?auth=...`
   to the database URL; CafePOS rejects URLs that contain query parameters or credentials.
5. Save the settings and press **Sync now**. The status line reports the last successful sync
   or the last error.
6. Review the mirrored data in the Firebase console under `cafepos`. Products, categories,
   today's summary, recent orders, daily sales, inventory, low-stock items and tables are
   included.

Firebase is a mirror for administrator viewing, not the main database. Continue to use the
POS and PostgreSQL for all live work. If the network or Firebase is unavailable, sales
continue normally and the next sync retries automatically.

## 14. Daily checklist

**Opening**

- Start the server PC and confirm the API service is running.
- Open a shift and count the opening float.
- Check low-stock alerts and today's reservations.

**During service**

- Send orders to the kitchen as they are taken.
- Watch the kitchen display and mark tickets ready/served.
- Keep cash and card payments separate.

**Closing**

- Complete or cancel all open orders.
- Count the drawer and close the shift.
- Print or export the day's sales report.
- Confirm the expected cash and investigate any variance.

## 15. Troubleshooting

| Problem | What to do |
| --- | --- |
| Cannot sign in | Check the server address, network, Caps Lock, and whether the account is locked or disabled. |
| Cannot take payment | A shift may not be open. Open a shift first. |
| Product missing from POS | Check that the product is available and has a price in Menu & Products. |
| Kitchen ticket missing | Check the item's preparation station and refresh the kitchen display. |
| Stock not decreasing | Confirm the product has a recipe and the inventory deduction event is configured. |
| Receipt does not print | Check the selected printer and cable/power; use the Windows print dialog as a fallback. |
| "Cannot reach server" | Ask the administrator to check the server service, firewall, and network. |
