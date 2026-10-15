# Release gates (not yet satisfied)

This project is still a development build. Passing disposable-database tests does not authorize shop transactions. Do not enter authoritative stock, customer debt or cash data into a demo/resettable dataset.

## Catalogue-only pilot

- [x] Add a default-off transaction feature gate. When off, Sales, Returns, Cash, Customers and Pricing are hidden in navigation/direct pages; their API routes reject requests. Raw stock-in/out is also blocked and hidden so it cannot bypass sale/receiving records; physical count and quarantine remain available. Backend `TransactionRelease:Enabled` and frontend `NEXT_PUBLIC_TRANSACTION_RELEASE_ENABLED` must both explicitly be `true` for development testing. Development startup does not auto-migrate or demo-seed. Focused gate tests passed; browser verification remains open.
- [ ] Establish a persistent local dataset separate from demo data. Disable demo seed/reset for it; review the target connection string before any migration or import.
- [ ] Make a backup, restore it to a separate disposable database, and verify rows and key relationships. Store a protected copy off the shop computer. Document the operator and recovery steps.
- [ ] Rehearse the complete pending migration chain on a populated copy of the prior schema. Compare part/customer IDs, row counts and sampled stock movement totals before and after; check for failed constraints. Do not upgrade the normal database until its reviewed backup exists and this rehearsal passes.
- [ ] Import 50 reviewed catalogue rows first; verify search, identity collisions, no generated stock/cash movements and no duplicates on re-import. Only then expand toward 1,000. Unknown stock must display `NotCounted`, never zero.
- [ ] Try at least 100 real part-number searches with family staff, including shared numbers, Mercedes prefix and `~/` suffix. Record wrong or slow results.

## Transaction release (later)

- [ ] Browser smoke against a **disposable, fully migrated copy**, not the normal/shop database. Verify as admin and employee: anonymous USD sale; LBP + Whish + debt; debt payment and correction; competing last-unit sale; sellable and quarantined linked returns; half-metre hose at USD/metre; page refresh during uncertain post; logout and employee switch. Inspect stock/account/cash records after each step. The newer return, opening-correction and pricing screens have not passed this check.
- [ ] Search older sales by number/date/customer/item/amount and process clearly labelled paper-era returns. Show admin exceptions for reversals, refunds, shortage sales and paper-era returns.
- [ ] Finish receipt numbering/reprinting and confirm invoice/tax requirements with the shop accountant before treating receipts as final.
- [ ] Implement and verify supplier payments, Whish fees/transfers, daily per-currency cash and wallet reconciliation, and a paper outage/back-entry procedure. Planned payments must never affect balances.
- [ ] Test a backup and restore that includes committed, unprinted transactions after the prior backup; choose and document acceptable data-loss window. Set up automatic monitored off-machine backups before authoritative transactions.
- [ ] Deploy and smoke-test the actual shop PC/LAN configuration, startup after reboot, role privacy, and operation recovery across tabs/terminals. Only then enable transaction routes for shop use.

## Known policy boundaries

- A second shortage sale waits for a physical count, as the owner explicitly decided. Employees may perform that count. Quarantined units are never a shortage override.
- Suggested selling prices hide actual costs in API responses, but a known cost-plus formula can reveal an approximate cost by inference. This approved convenience is not strict cost secrecy; the owner should review it before enabling staff use.
- Cut-hose counter entry is metres and USD/metre; stock/API posting is centimeters and USD/centimeter. Current conversion occurs in the frontend, so an end-to-end browser test and old-client compatibility check are required before release.
