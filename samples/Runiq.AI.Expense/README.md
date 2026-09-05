# Runiq Corporate Expense Analyst

This .NET 10 sample demonstrates how a Runiq agent selects, combines, and chains typed tools over corporate expense data stored in Excel workbooks.

## Scenario

A fictional multinational company needs repeatable answers about employee spend, budget performance, policy compliance, duplicate claims, unusual transactions, and procurement savings. The supplied workbooks are synthetic and intentionally contain both ordinary activity and documented analysis signals. They are educational sample data, not financial advice.

## Start with one tool

Ask:

> Find expense EXP-2026-0024 and show its original amount, reporting amount, and Excel source row.

The agent selects `expense_search` and returns one source-backed record:

- Original amount: `2,787.00 AED`
- Reporting amount: `24,246.90 TRY`
- Excel source: `Transactions-2026.xlsx:25`

```text
User question
     |
     v
Corporate Expense Analyst
     |
     | expenseId = EXP-2026-0024
     v
expense_search
     |
     v
EXP-2026-0024
Original:  2,787.00 AED
Reporting: 24,246.90 TRY
Source:    Transactions-2026.xlsx:25
```

## Combine multiple tools

Ask:

> Investigate expense EXP-2026-0001. First show its original and reporting amount, then check whether it violates any policy, whether it is anomalous, and show the budget variance for its department and month. Keep each result separate and include Excel source evidence.

The agent calls four tools and combines their evidence:

- `expense_search`: `6,200.00 USD`, normalized to `198,400.00 TRY`.
- `policy_violations`: `POL-06`; the amount exceeds the `40,000.00 TRY` category limit.
- `anomalous_expenses`: the amount exceeds the fixed `150,000 TRY` anomaly threshold.
- `budget_variance`: Customer Success (`D008`) is `70,660.40 TRY` over budget in January 2026 (`280,660.40 TRY` actual versus `210,000.00 TRY` budget).

```text
User: "Investigate EXP-2026-0001"
                 |
                 v
      Corporate Expense Analyst
                 |
       +---------+---------+------------------+
       |                   |                  |
       v                   v                  v
expense_search     policy_violations  anomalous_expenses
6,200 USD          POL-06             Above 150,000 TRY
= 198,400 TRY      limit exceeded     threshold
       |                   |                  |
       +-------------------+------------------+
                           |
                           v
                   budget_variance
                   D008 / 2026-01
                   Actual:   280,660.40 TRY
                   Budget:   210,000.00 TRY
                   Variance: +70,660.40 TRY
```

## Chain tool outputs

Ask:

> Search for Pending Training expenses in the Customer Success department during January 2026. Select the expense with the highest reporting amount from the search results. Use the selected expense ID to check whether it is anomalous. Only if an anomaly is confirmed, use the expense ID returned by the anomaly finding to calculate its department-month budget variance and preferred-vendor savings opportunity. Do not guess or hard-code an expense ID. Include Excel source evidence and explain how each tool output becomes the next input.

The first tool finds two records. The agent selects `EXP-2026-0001` because its `198,400 TRY` reporting amount is higher than `EXP-2026-0025` at `2,900 TRY`. That discovered ID becomes the next tool input. The anomaly result then conditionally enables the final two calls:

- Anomaly: confirmed because `198,400 TRY` exceeds the fixed `150,000 TRY` threshold.
- Filtered budget view: `201,300 TRY` actual versus `210,000 TRY` budget; `8,700 TRY` under budget.
- Preferred-vendor scenario: move eligible spend to `V001 — Atlas Travel`; estimated saving `15,872 TRY` at the illustrative 8% rate.

```text
expense_search
Customer Success + Training + Pending + Jan 2026
        |
        | returns EXP-2026-0001 and EXP-2026-0025
        v
Select highest reporting amount
        |
        | expenseId = EXP-2026-0001
        v
anomalous_expenses
        |
        | finding confirms EXP-2026-0001
        v
   +----+-----------------------+
   |                            |
   v                            v
budget_variance          cost_optimization
D008 / 2026-01           TRAVEL -> Atlas Travel
8,700 TRY UnderBudget    15,872 TRY estimated saving
```

## What the tools do

- `expense_search` finds expenses by ID, date, department, employee, vendor, category, original currency, or approval status and preserves original and reporting amounts.
- `budget_variance` groups matching expenses by department and month, then returns actual, budget, signed variance, and direction.
- `policy_violations` checks category amount limits, required receipts, and submission delays.
- `duplicate_expenses` finds exact matches and near matches within three days and a 2% normalized-amount difference.
- `anomalous_expenses` finds category z-score outliers and records at or above the fixed `150,000 TRY` threshold. The fixed threshold still applies when a filtered category has fewer than five records.
- `cost_optimization` groups eligible non-preferred-vendor spend and applies an illustrative 8% preferred-vendor savings assumption.

Tool results use one-based paging and return source expense IDs and workbook rows. An `ExpenseId` selects a record directly for search and policy checks; for aggregate tools it selects the duplicate pair, anomaly, department-month, or vendor group involving that expense without reducing the underlying calculation to one row.

## Architecture

- `Agents/` defines the agent instructions and registers six typed tools.
- `Tools/` contains six agent-facing capabilities, their calculations, and shared contracts.
- `Data/` discovers every `Transactions-*.xlsx` file and loads the master workbook through ClosedXML.
- `Domain/` contains the imported business records.
- `Configuration/` defines environment-aware workbook settings.
- `SampleData/` contains the copied-to-output source workbooks.

No custom HTTP endpoint is needed. The sample uses the existing Runiq dashboard at `/dashboard`.

The agent interprets the user's goal and selects one of six business tools. Each tool contains the calculation for the capability it exposes, keeping the Agent → Tool example direct and easy to follow.

## Workbook schemas and volume

`MasterData.xlsx` contains `Departments`, `Employees`, `Vendors`, `Categories`, `ExchangeRates`, `Budgets`, and `Policies`. It defines 8 departments, 40 employees, 25 vendors, 12 categories, 5 currencies, monthly reporting-currency budgets, effective-dated rates, and receipt/amount/submission rules.

`Transactions-2024.xlsx`, `Transactions-2025.xlsx`, and `Transactions-2026.xlsx` each contain a `Transactions` sheet. Together they contain 960 expenses over 36 months with four approval states. The master workbook contains 288 department-month budgets and exchange rates effective through the end of 2026. Transaction columns include expense ID, invoice reference, expense and submission dates, employee, department, vendor, category, original amount and currency, approval status, receipt flag, and description.

All sheets use typed dates/numbers/booleans, filterable Excel tables, frozen header rows, readable headers, and fitted columns. Import requires the exact documented worksheet set, table extent, ordered headers, and native Excel cell types; values are not silently coerced from another cell type.

## Methods and limitations

Amounts are normalized with the effective-dated `RateToReporting` value in `MasterData.xlsx`. The duplicate, anomaly, policy, budget, and savings rules are deliberately understandable demonstrations. They are not a complete audit, fraud model, tax opinion, observed vendor quotation, or procurement commitment.

These methods are deterministic demonstrations, not a complete audit, fraud model, tax opinion, or procurement commitment. Results depend on the supplied static exchange rates and synthetic records.

### Documented sample signals

- Monthly department budgets and transaction mix produce both over-budget and under-budget department-months across all three years.
- Exact duplicate pairs are `EXP-2024-0123`/`EXP-2024-0124`, `EXP-2025-0123`/`EXP-2025-0124`, and `EXP-2026-0123`/`EXP-2026-0124`.
- Near duplicate pairs are `EXP-2024-0246`/`EXP-2024-0247`, `EXP-2025-0246`/`EXP-2025-0247`, and `EXP-2026-0246`/`EXP-2026-0247`; each stays within the documented three-day and 2% rules.
- Every month includes deterministic missing-receipt and/or long-submission-lag candidates. Category thresholds add separate amount-limit violations.
- January and July include deliberately large transactions; category distributions also allow the z-score rule to surface statistical outliers.
- Four of every five vendors in each Travel, Office, Digital, Services, and Supply consolidation group are non-preferred; the remaining vendor is the explicit preferred target for that same comparable group.

## Configure and run

Enter a development API key only in `appsettings.Development.json`:

```json
{
  "OpenAI": {
    "ApiKey": ""
  }
}
```

Then run:

```powershell
dotnet restore Runiq.AI.slnx
dotnet build Runiq.AI.slnx --no-restore
dotnet run --project samples/Runiq.AI.Expense/Runiq.AI.Expense.csproj
```

Open the URL shown by ASP.NET Core, browse to `/dashboard`, and select **Corporate Expense Analyst**. Startup imports and validates the workbooks before the dashboard accepts requests. A missing key prevents live OpenAI conversations but does not change workbook calculations.

## Expected tool behavior

The agent selects one of `expense_search`, `budget_variance`, `policy_violations`, `duplicate_expenses`, `anomalous_expenses`, or `cost_optimization`. Expense IDs can be searched directly. Date inputs use `yyyy-MM-dd`; department, employee, vendor, and category filters accept either workbook IDs or display names. `OriginalCurrency` filters the transaction currency and is distinct from the fixed reporting currency. A filter with no matches returns a successful empty result. Evidence includes record IDs, source rows, original currency where applicable, normalized amounts, and the rule or threshold used.

## Example prompts

1. Which approved 2026 expenses should the CFO review first because they have the highest reporting-currency value?
2. Show the source transactions behind Sales travel spend in Q1 2026, preserving original currencies and normalized amounts.
3. Which department-months exceeded budget in 2026, ranked by the absolute size of the unfavorable variance?
4. Where did Engineering finish furthest under budget in 2026, and which source expenses support each result?
5. Explain the June 2026 budget position for Marketing, including actual, budget, signed variance, and direction.
6. Which 2026 claims violate receipt policy, and what rule was applied to each claim?
7. Which expenses exceeded their category amount limits in 2026, ranked by reporting-currency exposure?
8. Which employees submitted claims later than policy permits, and how many days late was each submission?
9. Show all policy findings for Finance in the second half of 2026 so the controller can prioritize follow-up.
10. Find exact duplicate claims across all three years and show the evidence that makes each pair exact.
11. Find near-duplicate claims for employee E007 and explain the date and normalized-amount differences.
12. Investigate `EXP-2026-0123` and identify any exact duplicate with its source rows.
13. Investigate `EXP-2026-0246` and identify any near duplicate under the configured matching rules.
14. Which Software expenses are statistical anomalies, and what category mean, standard deviation, and z-score support each finding?
15. Which 2026 expenses crossed the TRY 150,000 large-value threshold, regardless of statistical score?
16. Show anomaly evidence for Travel while preserving both original-currency and reporting-currency amounts.
17. Where could Travel spend move from non-preferred vendors to a comparable preferred vendor, and what is the estimated 8% savings scenario?
18. Show procurement consolidation opportunities for Marketing, including the proposed preferred vendor and bounded source evidence.
19. Which vendor groups offer the largest estimated savings, and clearly separate current spend, scenario savings, and assumptions?
20. Show rejected EUR expenses in 2026 so Finance can review the original values alongside normalized amounts.
21. Retrieve pending claims for Operations in December 2026 with expense IDs and workbook source rows for operational follow-up.
22. Show approved lodging claims for Customer Success in 2025, including receipt status and submission dates.
23. Are there any transactions for vendor V025 in January 2024? Return a successful empty result if none exist.
24. List the first page of 2026 Professional Services expenses and include the total finding count so I can plan the review workload.
