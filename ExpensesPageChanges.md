# ExpensesPageChanges.md

This document describes **what changed in the Expenses page (ExpenseTrackerPage)** compared to the previous existing implementation, and explains **what each field/property/parameter in the page + view model means, why it exists, and how it is used**.

> Note: This repo snapshot shows the current implementation of `ExpenseTrackerPage.xaml`, `ExpenseTrackerPage.xaml.cs`, and `ExpenseTrackerViewModel.cs`. If the “previous” version is not available in your working directory, the “compared to what already existed” section is written as **the behavioral deltas implied by the current code** (new/changed UI elements and view-model capabilities).

---

## Files involved

- **UI (XAML)**: `Ritchie/src/Richie.UI/Views/Pages/ExpenseTrackerPage.xaml`
- **UI code-behind**: `Ritchie/src/Richie.UI/Views/Pages/ExpenseTrackerPage.xaml.cs`
- **ViewModel**: `Ritchie/src/Richie.UI/ViewModels/ExpenseTrackerViewModel.cs`
- **DTOs / filter models (used by this page)**:
  - `Ritchie/src/Richie.Application/Expenses/ExpenseDtos.cs`
  - `Ritchie/src/Richie.Application/Expenses/ExpenseCategoryNames.cs`
  - `Ritchie/src/Richie.Application/Expenses/ExpenseImportColumns.cs`
  - `Ritchie/src/Richie.Domain/Expenses/ExpenseBudget.cs` (used by budget/recurring features elsewhere; not directly shown in the expenses UI code)

---

## Summary of changes (Expenses page)

### 1) Added a “rich” Expenses dashboard section (KPIs + insights)
The page now shows multiple **KPI cards** plus an **Insights** card:

- “Spent This Month”
- “Income This Month”
- “Top Category”
- “Insights” (list)

These KPIs are not static labels; they are bound to view-model properties:

- `CurrentMonthText`
- `IncomeThisMonthText`
- `TopCategoryText`
- `Insights`
- `MonthOverMonthText` (shown under the spent KPI)

**Why it exists**: to provide a quick monthly view and higher-level signals (trend/insight) without requiring the user to scan the full transaction grid.

---

### 2) Added an “Income vs expense trend” chart
A LiveCharts `CartesianChart` is bound to:

- `IncomeExpenseSeries` (two trend lines)
- `IncomeExpenseAxes` (x-axis labels)

The chart is labeled:

- “Income vs expense trend (last 6 months)”

**Why it exists**: expenses are easier to understand when shown in time context, and comparing against income highlights net pressure.

---

### 3) Added filter bar with multiple filter types
A new filter row allows the user to filter expense history using:

- **Text search** (`SearchBox` bound to `SearchText`)
- **Category filter** (`ComboBox` bound to `SelectedCategory`)
- **Date range** (`FromDate`, `ToDate`)
- **Amount range** (`MinAmountText`, `MaxAmountText`)

The filter is applied via:

- **Apply** button → `OnApplyFilter` → `Vm.ApplyFilter()`
- **Clear** button → `OnClearFilter` → `Vm.ClearFilter()`

Additionally, **typing into Search** triggers `OnSearchTextChanged` which calls `Vm.ApplyFilter()` in near-real-time.

**Why it exists**: users need to find and analyze specific expenses (by category, timeframe, and amount) without exporting.

---

### 4) Replaced/implemented the expense history grid with explicit columns + action buttons
The `DataGrid` (`ExpenseGrid`) binds to:

- `Items` (collection of `ExpenseSummary`)

Columns are explicitly declared and include:

- Date
- Category (category name)
- Amount (formatted as `₹{0:N2}`)
- SpentBy
- SpentFor
- Recurring (checkbox)
- Actions (buttons: Bills/receipt, Edit, Delete)

**Why it exists**: gives consistent, readable expense history with direct CRUD/actions.

---

### 5) Added support for actions via separate windows
The code-behind wires buttons to modals / windows and refreshes the view model when needed:

- **Add expense** → `AddEditExpenseWindow`
- **Edit expense** → `AddEditExpenseWindow` with an ID
- **Delete expense** → confirms then calls `Vm.Delete(id)`
- **Recurring** → `RecurringExpensesWindow`, then `Vm.Refresh()`
- **Analytics** → `ExpenseAnalyticsWindow`, then `Vm.Refresh()`
- **Bulk upload** → `BulkUploadWindow`; calls `Vm.Refresh()` only if imported anything
- **Income** → `IncomeWindow`; calls `Vm.Refresh()` (because KPIs/chart depend on income too)
- **Bills & receipts** → `BillsWindow`; initializes it with `(id, "Bills & receipts")`

**Why it exists**: keeps the page focused while delegating specialized flows.

---

## Detailed field/property guide (meaning, use, why it exists)

Below is a field-by-field explanation for the **properties used by the XAML UI bindings**.

### A) Dashboard/KPI fields

#### `CurrentMonthText : string`
- **Where used**: “Spent This Month” KPI card value.
- **Set in**: `ExpenseTrackerViewModel.Refresh()`
- **How calculated**:
  - `ExpenseDashboard dash = _expenses.GetDashboard()`
  - `CurrentMonthText = Money(dash.CurrentMonthTotal)`
- **Why it exists**: shows the aggregate expense total for the current month.

#### `MonthOverMonthText : string`
- **Where used**: second line under “Spent This Month”.
- **How calculated**:
  - If `dash.LastMonthTotal > 0`:
    - Displays `+X%` or `-X%` vs last month using `dash.MonthOverMonthPercent`
  - Else:
    - Shows: “No spending last month to compare”
- **Why it exists**: helps users understand whether spending is rising or falling.

#### `IncomeThisMonthText : string`
- **Where used**: “Income This Month” KPI card.
- **Set in**: `Refresh()` using `_income.GetMonthlyTotal()`.
- **Why it exists**: supports income vs expense context.

#### `TopCategoryText : string`
- **Where used**: “Top Category” KPI card.
- **How set**:
  - `dash.TopCategoryName ?? "—"`
- **Why it exists**: highlights where spending concentrates.

#### `Insights : ObservableCollection<string>`
- **Where used**: “Insights” card via `ItemsControl ItemsSource="{Binding Insights}"`.
- **Set in**: `Refresh()` using `dash.Insights`.
- **Why it exists**: turns raw aggregates into user-facing conclusions (e.g., “Your spending increased in Groceries”).

---

### B) Chart fields (Income vs Expense trend)

#### `IncomeExpenseSeries : ISeries[]`
- **Where used**: bound to `lvc:CartesianChart Series`.
- **How built**: `BuildIncomeExpenseChart()` creates:
  - a “Income” trend line from `_income.GetMonthlyTotals(6)`
  - an “Expense” trend line from `_analytics.GetMonthlyTotals(6)`
- **Why it exists**: renders the visual time comparison.

Each series uses `TrendLine(...)` which sets:
- `Name` → series label (“Income” / “Expense”)
- `Values` → numeric amounts per month
- `Stroke`, `GeometryStroke` → line color + thickness
- `GeometryFill` → point fill color
- `GeometrySize` → marker size
- `Fill = null` → only line, no area fill
- `LineSmoothness = 0.6` → smoother visual trend

#### `IncomeExpenseAxes : Axis[]`
- **Where used**: bound to `lvc:CartesianChart XAxes`.
- **How built**:
  - x-axis labels come from `income.Select(d => d.Label)`
- **Why it exists**: maps the 6 months into readable labels on the chart.

---

### C) History grid fields

#### `Items : ObservableCollection<ExpenseSummary>`
- **Where used**: `DataGrid ItemsSource="{Binding Items}"`.
- **How set**: `ApplyFilter()` calls:
  - `_expenses.GetExpenses(filter)`
  - wraps result in `ObservableCollection`.
- **Why it exists**: this is the primary transactional data shown on the page.

#### `ExpenseSummary` fields (from `ExpenseDtos.cs`)
The page binds these properties inside grid columns:

- `Id : Guid`
  - **Used for**: action button Tag bindings (`Tag="{Binding Id}"`) so code-behind knows which expense to edit/delete/open bills for.
  - **Why exists**: stable identifier for CRUD operations.

- `Date : DateTime`
  - **Used for**: “Date” column binding `StringFormat=d`.
  - **Why exists**: sorting/filtering and historical record.

- `Amount : decimal`
  - **Used for**: “Amount” column with currency formatting.
  - **Why exists**: monetary value being tracked.

- `Category : ExpenseCategory` and `CategoryName : string`
  - **Used for**:
    - filter logic uses `Category` when building `ExpenseFilter`
    - “Category” column displays `CategoryName`
  - **Why exists**: separate enum (logic) vs display name (UI text).

- `SpentBy : string?`
  - **Used for**: “Spent by” column.
  - **Why exists**: supports attribution (e.g., who paid) or contextual spending.

- `SpentFor : string?`
  - **Used for**: “Spent for” column.
  - **Why exists**: supports descriptive spending purpose.

- `IsRecurring : bool`
  - **Used for**: “Recurring” checkbox in the grid.
  - **Why exists**: lets the user see recurring expenses and (in the UI) potentially toggle/indicate recurrence.

---

### D) Filter fields

These properties drive `ApplyFilter()`.

#### `SearchText : string`
- **Where used**:
  - TextBox `Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"`
  - `OnSearchTextChanged` calls `ApplyFilter()`.
- **How applied**:
  - `Search = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText`
- **Why it exists**: quick narrowing by keywords.

#### `SelectedCategory : CategoryFilterOption?`
- **Where used**:
  - Category ComboBox `ItemsSource="{Binding CategoryFilters}" DisplayMemberPath="Text" SelectedItem="{Binding SelectedCategory}"`
- **How applied**:
  - `Category: SelectedCategory?.Value` (nullable)
- **Why it exists**: the view shows friendly text; the filter uses the enum value.

##### `CategoryFilterOption` record
- `Value : ExpenseCategory?` and `Text : string`
- Constructed as:
  - “All categories” option with `Value = null`
  - One option per `ExpenseCategory` with `ExpenseCategoryNames.Display(c)`.

#### `FromDate : DateTime?`
- **Where used**: From DatePicker.
- **How applied**: passed to `ExpenseFilter.From`.
- **Why exists**: start boundary for history.

#### `ToDate : DateTime?`
- **Where used**: To DatePicker.
- **How applied**: passed to `ExpenseFilter.To`.
- **Why exists**: end boundary for history.

#### `MinAmountText : string` and `MaxAmountText : string`
- **Where used**: Min/Max TextBoxes.
- **How applied**:
  - parsed via `ParseNullable(MinAmountText)` and `ParseNullable(MaxAmountText)`
  - `decimal.TryParse(..., CultureInfo.CurrentCulture, out v)`
- **Why exists**:
  - TextBoxes allow user entry in UI culture format
  - Filter uses actual decimals (numeric) to compare amounts.

---

## Why these view-model operations exist

### `Refresh()`
Purpose: update all dependent dashboard/chart/grid content.

- Gets `ExpenseDashboard` from `_expenses.GetDashboard()`.
- Gets income from `_income.GetMonthlyTotal()`.
- Sets KPI strings.
- Sets breakdown + insights.
- Builds chart.
- Calls `ApplyFilter()` at the end so the grid reflects current filter state.

### `ApplyFilter()`
Purpose: compute the filtered `Items` collection.

- Builds an `ExpenseFilter` object:
  - `From`, `To`, `Category`, `MinAmount`, `MaxAmount`, `Search`
- Assigns:
  - `Items = new ObservableCollection<ExpenseSummary>(_expenses.GetExpenses(filter))`

### `ClearFilter()`
Purpose: reset UI filters to defaults.

- `SearchText = string.Empty`
- `SelectedCategory = CategoryFilters[0]` (the “All categories” option)
- `FromDate = null`, `ToDate = null`
- `MinAmountText = string.Empty`, `MaxAmountText = string.Empty`
- then `ApplyFilter()`.

---

## Input/DTO explanation (used by this page)

### `ExpenseFilter` (from `ExpenseDtos.cs`)
All fields are optional so the same endpoint can serve many filter combinations.

- `From : DateTime?` → lower date bound
- `To : DateTime?` → upper date bound
- `Category : ExpenseCategory?` → category enum (null means all)
- `MinAmount : decimal?` → min amount constraint
- `MaxAmount : decimal?` → max amount constraint
- `Search : string?` → free-text query (null means no search)

### `ExpenseSummary` (grid row)
Explained above; it’s the projection used for the list view.

### `ExpenseDashboard`
Used for KPI/insights and chart breakdown.
- `CurrentMonthTotal`
- `LastMonthTotal`
- `MonthOverMonthPercent`
- `TopCategoryName`
- `CurrentMonthBreakdown` (used by `Breakdown`)
- `Recent` (not directly bound in the current XAML)
- `Insights` (bound to UI)

---

## Event/interaction details (code-behind)

### Search live filtering
- `OnSearchTextChanged` calls `Vm.ApplyFilter()`.
- This keeps the grid responsive without a separate “Search” button.

### DataGrid embedded controls click behavior
- `OnExpenseGridPreviewMouseLeftButtonDown` is a “no-op” handler with comments explaining why it exists.
- **Goal**: ensure embedded controls (checkbox/buttons) behave properly inside a DataGrid.

### Button handlers
Each button handler opens a dialog/window, sets `Owner`, calls `ShowDialog()`, and then refreshes the view-model when underlying expense/income data may have changed.

---

## Recurring checkbox note (important behavior)
The DataGrid Recurring column binds:

- `IsChecked="{Binding IsRecurring, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"`

In the provided code, the view-model does not expose a dedicated command to persist this toggle immediately; instead, the grid is backed by `ExpenseSummary` returned from `_expenses.GetExpenses(filter)`.

**Why this matters**:
- Either the underlying data/service returns `ExpenseSummary.IsRecurring` and the UI is intended to be read-only visually,
- or additional logic exists elsewhere to persist recurrence changes (not shown in the current file).

---

## File added

- `Ritchie/ExpensesPageChanges.md`

---

## Conclusion
The Expenses page is now a cohesive “analytics + filtering + history + actions” screen:
- a KPI/insights dashboard,
- a 6-month income-vs-expense trend chart,
- a multi-criteria filter bar,
- a transaction grid with action buttons (bills/receipt, edit, delete).

All UI elements are driven by `ExpenseTrackerViewModel`, which in turn uses:
- `IExpenseService` for dashboard + filtered expenses,
- `IIncomeService` for income totals and chart data,
- `IExpenseAnalyticsService` for expense trend data.

