using ClosedXML.Excel;
using Microsoft.Extensions.Options;
using Runiq.AI.Expense.Configuration;
using Runiq.AI.Expense.Domain;

namespace Runiq.AI.Expense.Data;

/// <summary>Discovers, imports, and globally validates the Excel workbooks used by the sample.</summary>
public sealed class ExcelExpenseDataSource
{
    private static readonly SheetSpec[] MasterSheets =
    [
        new("Departments", ["DepartmentId", "Name"]),
        new("Employees", ["EmployeeId", "Name", "DepartmentId", "Title"]),
        new("Vendors", ["VendorId", "Name", "Preferred", "ConsolidationGroup"]),
        new("Categories", ["CategoryId", "Name"]),
        new("ExchangeRates", ["Currency", "RateToReporting", "EffectiveFrom", "EffectiveTo"]),
        new("Budgets", ["DepartmentId", "Period", "AmountReporting"]),
        new("Policies", ["RuleId", "CategoryId", "MaximumReportingAmount", "ReceiptRequired", "MaximumSubmissionDays"])
    ];

    private static readonly SheetSpec TransactionSheet = new("Transactions", ["ExpenseId", "InvoiceReference", "ExpenseDate", "SubmittedDate", "EmployeeId", "DepartmentId", "VendorId", "CategoryId", "Amount", "Currency", "ApprovalStatus", "HasReceipt", "Description"]);

    private readonly string dataDirectory;
    private readonly string reportingCurrency;
    private readonly int maxWorkbookCount;
    private readonly int maxRowsPerSheet;

    /// <summary>Initializes the Excel data source from environment-aware configuration.</summary>
    /// <param name="environment">Web host environment used to resolve relative paths.</param>
    /// <param name="options">Validated expense data settings.</param>
    public ExcelExpenseDataSource(IWebHostEnvironment environment, IOptions<ExpenseDataOptions> options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);
        dataDirectory = Path.GetFullPath(RequiredText(options.Value.Directory, "ExpenseData:Directory"), environment.ContentRootPath);
        reportingCurrency = IdentifierSemantics.Canonicalize(RequiredText(options.Value.ReportingCurrency, "ExpenseData:ReportingCurrency"));
        maxWorkbookCount = InRange(options.Value.MaxWorkbookCount, 2, 100, "ExpenseData:MaxWorkbookCount");
        maxRowsPerSheet = InRange(options.Value.MaxRowsPerSheet, 1, 100_000, "ExpenseData:MaxRowsPerSheet");
    }

    /// <summary>Loads all master and transaction workbooks using category-wide validation passes.</summary>
    /// <returns>A validated in-memory data set, including a valid empty transaction set when source sheets have no data rows.</returns>
    /// <exception cref="InvalidDataException">Thrown when discovery, read, schema, cell, key, reference, or domain validation fails.</exception>
    public ExpenseDataSet Load()
    {
        var paths = DiscoverPaths();
        var workbooks = OpenAll(paths);
        try
        {
            ValidateAllSchemas(workbooks);
            var staged = ReadAllCells(workbooks);
            ValidateAllDuplicates(staged);
            var indexed = Index(staged);
            ValidateAllReferences(indexed);
            ValidateAllDomainRules(indexed);
            return new ExpenseDataSet(reportingCurrency, indexed.Departments, indexed.Employees, indexed.Vendors, indexed.Categories, indexed.Rates.Select(row => row.Value).ToArray(), indexed.Budgets.Select(row => row.Value).ToArray(), indexed.Policies.Select(row => row.Value).ToArray(), indexed.Transactions.Select(row => row.Value).ToArray());
        }
        finally
        {
            foreach (var workbook in workbooks) workbook.Workbook.Dispose();
        }
    }

    /// <summary>Discovers the required workbook set and enforces its configured size bound.</summary>
    /// <returns>Master and transaction paths in deterministic order.</returns>
    private IReadOnlyList<string> DiscoverPaths()
    {
        if (!Directory.Exists(dataDirectory)) throw new InvalidDataException("The configured expense data directory was not found.");
        var masterPath = Path.Combine(dataDirectory, "MasterData.xlsx");
        var transactionPaths = Directory.GetFiles(dataDirectory, "Transactions-*.xlsx").OrderBy(path => path, StringComparer.Ordinal).ToArray();
        if (!File.Exists(masterPath) || transactionPaths.Length == 0) throw new InvalidDataException("The configured expense data directory does not contain the required workbooks.");
        var paths = new[] { masterPath }.Concat(transactionPaths).ToArray();
        if (paths.Length > maxWorkbookCount) throw new InvalidDataException($"Workbook count {paths.Length} exceeds the configured maximum of {maxWorkbookCount}.");
        return paths;
    }

    /// <summary>Opens every discovered workbook before any lower-priority validation begins.</summary>
    /// <param name="paths">Workbook paths.</param>
    /// <returns>Opened workbook handles.</returns>
    private static IReadOnlyList<WorkbookHandle> OpenAll(IReadOnlyList<string> paths)
    {
        var workbooks = new List<WorkbookHandle>(paths.Count);
        try
        {
            foreach (var path in paths)
            {
                try { workbooks.Add(new WorkbookHandle(path, new XLWorkbook(path))); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException or InvalidOperationException)
                {
                    throw new InvalidDataException($"Workbook '{Path.GetFileName(path)}' could not be read.", exception);
                }
            }
            return workbooks;
        }
        catch
        {
            foreach (var workbook in workbooks) workbook.Workbook.Dispose();
            throw;
        }
    }

    /// <summary>Validates every workbook sheet and exact table/header schema before cell parsing.</summary>
    /// <param name="workbooks">Opened workbooks.</param>
    private void ValidateAllSchemas(IReadOnlyList<WorkbookHandle> workbooks)
    {
        foreach (var workbook in workbooks)
        {
            SheetSpec[] expected = IsMaster(workbook.Path) ? MasterSheets : [TransactionSheet];
            var actualNames = workbook.Workbook.Worksheets.Select(sheet => sheet.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            var expectedNames = expected.Select(sheet => sheet.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (!actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal)) throw new InvalidDataException($"Workbook '{Path.GetFileName(workbook.Path)}' has an unexpected worksheet set. Expected: {string.Join(", ", expectedNames)}.");
            foreach (var spec in expected) ValidateSheetSchema(workbook, spec);
        }
    }

    /// <summary>Validates one sheet's used content range, table range, header sequence, and row bound.</summary>
    /// <param name="workbook">Owning workbook.</param><param name="spec">Expected sheet schema.</param>
    private void ValidateSheetSchema(WorkbookHandle workbook, SheetSpec spec)
    {
        var sheet = workbook.Workbook.Worksheet(spec.Name);
        var used = sheet.RangeUsed(XLCellsUsedOptions.Contents) ?? throw SchemaError(workbook.Path, spec.Name, "the sheet is empty");
        if (used.RangeAddress.FirstAddress.RowNumber != 1 || used.RangeAddress.FirstAddress.ColumnNumber != 1) throw CellError(workbook.Path, spec.Name, used.RangeAddress.FirstAddress.RowNumber, used.RangeAddress.FirstAddress.ColumnNumber, "used content must start at A1");
        if (used.ColumnCount() != spec.Headers.Length)
        {
            var offendingColumn = used.ColumnCount() > spec.Headers.Length ? spec.Headers.Length + 1 : used.ColumnCount() + 1;
            throw CellError(workbook.Path, spec.Name, 1, offendingColumn, $"schema must contain exactly {spec.Headers.Length} columns but contains {used.ColumnCount()}");
        }
        var tables = sheet.Tables.ToArray();
        if (tables.Length != 1 || tables[0].RangeAddress.ToString() != used.RangeAddress.ToString()) throw CellError(workbook.Path, spec.Name, 1, 1, "the single Excel table must exactly cover the used header and data range");
        if (used.RowCount() - 1 > maxRowsPerSheet) throw SchemaError(workbook.Path, spec.Name, $"data row count {used.RowCount() - 1} exceeds the configured maximum of {maxRowsPerSheet}");
        var actualHeaders = new string[used.ColumnCount()];
        for (var column = 1; column <= used.ColumnCount(); column++)
        {
            var cell = sheet.Cell(1, column);
            if (cell.DataType != XLDataType.Text) throw CellError(workbook.Path, spec.Name, 1, column, "must be text");
            actualHeaders[column - 1] = cell.GetString().Trim();
            if (!actualHeaders[column - 1].Equals(spec.Headers[column - 1], StringComparison.Ordinal)) throw CellError(workbook.Path, spec.Name, 1, column, $"expected header '{spec.Headers[column - 1]}' but found '{actualHeaders[column - 1]}'");
        }
        if (actualHeaders.Distinct(StringComparer.Ordinal).Count() != spec.Headers.Length) throw CellError(workbook.Path, spec.Name, 1, 1, "header names must be unique");
    }

    /// <summary>Parses and type-checks all required cells across the workbook set.</summary>
    /// <param name="workbooks">Schema-valid workbooks.</param>
    /// <returns>Staged typed rows retaining source context.</returns>
    private static StagedData ReadAllCells(IReadOnlyList<WorkbookHandle> workbooks)
    {
        var master = workbooks.Single(workbook => IsMaster(workbook.Path));
        var staged = new StagedData(
            ReadRows(master, "Departments", row => new Department(Text(row, 1), Text(row, 2))),
            ReadRows(master, "Employees", row => new Employee(Text(row, 1), Text(row, 2), Text(row, 3), Text(row, 4))),
            ReadRows(master, "Vendors", row => new Vendor(Text(row, 1), Text(row, 2), Boolean(row, 3), Text(row, 4))),
            ReadRows(master, "Categories", row => new ExpenseCategory(Text(row, 1), Text(row, 2))),
            ReadRows(master, "ExchangeRates", row => new ExchangeRate(Text(row, 1), PositiveDecimal(row, 2), Date(row, 3), Date(row, 4))),
            ReadRows(master, "Budgets", row => new Budget(Text(row, 1), Date(row, 2), PositiveDecimal(row, 3))),
            ReadRows(master, "Policies", row => new PolicyRule(Text(row, 1), Text(row, 2), PositiveDecimal(row, 3), Boolean(row, 4), NonNegativeInt(row, 5))),
            []);
        foreach (var workbook in workbooks.Where(workbook => !IsMaster(workbook.Path)))
        {
            staged.Transactions.AddRange(ReadRows(workbook, "Transactions", row => new ExpenseTransaction(Text(row, 1), Text(row, 2), Date(row, 3), Date(row, 4), Text(row, 5), Text(row, 6), Text(row, 7), Text(row, 8), PositiveDecimal(row, 9), Text(row, 10), EnumValue<ApprovalStatus>(row, 11), Boolean(row, 12), Text(row, 13), Path.GetFileName(row.Path), row.RowNumber)));
        }
        return staged;
    }

    /// <summary>Reads all data rows from a schema-valid worksheet.</summary>
    /// <typeparam name="T">Parsed row type.</typeparam><param name="workbook">Workbook handle.</param><param name="sheetName">Worksheet name.</param><param name="factory">Typed row parser.</param><returns>Rows with source context.</returns>
    private static List<ImportedRow<T>> ReadRows<T>(WorkbookHandle workbook, string sheetName, Func<RowContext, T> factory)
    {
        var sheet = workbook.Workbook.Worksheet(sheetName);
        var lastRow = sheet.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 1;
        var rows = new List<ImportedRow<T>>(Math.Max(0, lastRow - 1));
        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var context = new RowContext(workbook.Path, sheetName, sheet.Row(rowNumber), rowNumber);
            rows.Add(new ImportedRow<T>(factory(context), workbook.Path, sheetName, rowNumber));
        }
        return rows;
    }

    /// <summary>Validates all unique key contracts before reference or domain validation.</summary>
    /// <param name="data">Staged data.</param>
    private static void ValidateAllDuplicates(StagedData data)
    {
        EnsureUnique(data.Departments, row => row.Value.Id, "DepartmentId");
        EnsureUnique(data.Employees, row => row.Value.Id, "EmployeeId");
        EnsureUnique(data.Vendors, row => row.Value.Id, "VendorId");
        EnsureUnique(data.Categories, row => row.Value.Id, "CategoryId");
        EnsureUnique(data.Budgets, row => $"{row.Value.DepartmentId}:{row.Value.Period:yyyy-MM}", "department-month budget");
        EnsureUnique(data.Policies, row => row.Value.RuleId, "RuleId");
        EnsureUnique(data.Transactions, row => row.Value.Id, "ExpenseId");
    }

    /// <summary>Builds lookup indexes after all duplicate checks have completed.</summary>
    /// <param name="data">Duplicate-free staged data.</param><returns>Indexed data.</returns>
    private static IndexedData Index(StagedData data) => new(
        data.Departments.ToDictionary(row => row.Value.Id, row => row.Value, StringComparer.OrdinalIgnoreCase),
        data.Employees.ToDictionary(row => row.Value.Id, row => row.Value, StringComparer.OrdinalIgnoreCase),
        data.Vendors.ToDictionary(row => row.Value.Id, row => row.Value, StringComparer.OrdinalIgnoreCase),
        data.Categories.ToDictionary(row => row.Value.Id, row => row.Value, StringComparer.OrdinalIgnoreCase),
        data.Rates, data.Budgets, data.Policies, data.Transactions);

    /// <summary>Validates every foreign-key relationship before domain rules.</summary>
    /// <param name="data">Indexed data.</param>
    private static void ValidateAllReferences(IndexedData data)
    {
        foreach (var employee in data.Employees.Values)
            if (!data.Departments.ContainsKey(employee.DepartmentId)) throw new InvalidDataException($"Employee '{employee.Id}' references unknown department '{employee.DepartmentId}'.");
        foreach (var row in data.Budgets)
            if (!data.Departments.ContainsKey(row.Value.DepartmentId)) throw ReferenceError(row, "department", row.Value.DepartmentId);
        foreach (var row in data.Policies)
            if (!row.Value.CategoryId.Equals("ALL", StringComparison.OrdinalIgnoreCase) && !data.Categories.ContainsKey(row.Value.CategoryId)) throw ReferenceError(row, "category", row.Value.CategoryId);
        foreach (var row in data.Transactions)
        {
            var transaction = row.Value;
            if (!data.Employees.ContainsKey(transaction.EmployeeId)) throw ReferenceError(row, "employee", transaction.EmployeeId);
            if (!data.Departments.ContainsKey(transaction.DepartmentId)) throw ReferenceError(row, "department", transaction.DepartmentId);
            if (!data.Vendors.ContainsKey(transaction.VendorId)) throw ReferenceError(row, "vendor", transaction.VendorId);
            if (!data.Categories.ContainsKey(transaction.CategoryId)) throw ReferenceError(row, "category", transaction.CategoryId);
            if (!data.Rates.Any(rate => rate.Value.Currency.Equals(transaction.Currency, StringComparison.OrdinalIgnoreCase) && transaction.ExpenseDate >= rate.Value.EffectiveFrom && transaction.ExpenseDate <= rate.Value.EffectiveTo)) throw ReferenceError(row, "exchange rate currency", transaction.Currency);
        }
    }

    /// <summary>Validates date, rate, employee ownership, and complete budget coverage rules.</summary>
    /// <param name="data">Reference-valid indexed data.</param>
    private static void ValidateAllDomainRules(IndexedData data)
    {
        foreach (var row in data.Rates)
            if (row.Value.EffectiveTo < row.Value.EffectiveFrom) throw DomainError(row, $"exchange rate '{row.Value.Currency}' has an invalid effective date range");
        var overlap = data.Rates.GroupBy(row => row.Value.Currency, StringComparer.OrdinalIgnoreCase).SelectMany(group => group.SelectMany((left, index) => group.Skip(index + 1).Select(right => (left, right)))).FirstOrDefault(pair => pair.left.Value.EffectiveFrom <= pair.right.Value.EffectiveTo && pair.right.Value.EffectiveFrom <= pair.left.Value.EffectiveTo);
        if (overlap != default) throw DomainError(overlap.left, $"exchange rate periods overlap for currency '{overlap.left.Value.Currency}'");
        foreach (var row in data.Transactions)
        {
            var transaction = row.Value;
            if (!data.Employees[transaction.EmployeeId].DepartmentId.Equals(transaction.DepartmentId, StringComparison.OrdinalIgnoreCase)) throw DomainError(row, $"department does not match employee '{transaction.EmployeeId}'");
            if (transaction.SubmittedDate < transaction.ExpenseDate || transaction.SubmittedDate > transaction.ExpenseDate.AddDays(120)) throw DomainError(row, "submission date is outside the allowed expense-date window");
        }
        var budgetKeys = data.Budgets.Select(row => (DepartmentId: IdentifierSemantics.Canonicalize(row.Value.DepartmentId), row.Value.Period)).ToHashSet();
        var missingBudget = data.Transactions.Select(row => new { Row = row, Key = (DepartmentId: IdentifierSemantics.Canonicalize(row.Value.DepartmentId), Period: new DateOnly(row.Value.ExpenseDate.Year, row.Value.ExpenseDate.Month, 1)) }).FirstOrDefault(item => !budgetKeys.Contains(item.Key));
        if (missingBudget is not null) throw DomainError(missingBudget.Row, $"no budget exists for department '{missingBudget.Key.DepartmentId}' and period '{missingBudget.Key.Period:yyyy-MM}'");
    }

    /// <summary>Ensures a key appears only once and reports both source locations.</summary>
    /// <typeparam name="T">Row value type.</typeparam><param name="rows">Rows to inspect.</param><param name="keySelector">Stable key selector.</param><param name="keyName">Key label.</param>
    private static void EnsureUnique<T>(IEnumerable<ImportedRow<T>> rows, Func<ImportedRow<T>, string> keySelector, string keyName)
    {
        var duplicate = rows.GroupBy(keySelector, StringComparer.OrdinalIgnoreCase).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null) throw new InvalidDataException($"Duplicate {keyName} '{duplicate.Key}' exists at {string.Join(", ", duplicate.Select(Location))}.");
    }

    /// <summary>Reads a required text cell without coercing another Excel type.</summary>
    /// <param name="row">Source row.</param><param name="column">One-based column number.</param><returns>Trimmed text.</returns>
    private static string Text(RowContext row, int column)
    {
        var cell = row.Row.Cell(column);
        if (cell.DataType != XLDataType.Text) throw CellError(row.Path, row.SheetName, row.RowNumber, column, "must be text");
        var value = cell.GetString().Trim();
        return value.Length == 0 ? throw CellError(row.Path, row.SheetName, row.RowNumber, column, "is required") : value;
    }

    /// <summary>Reads a strictly numeric positive decimal cell.</summary>
    /// <param name="row">Source row.</param><param name="column">One-based column number.</param><returns>Positive decimal.</returns>
    private static decimal PositiveDecimal(RowContext row, int column)
    {
        var cell = row.Row.Cell(column);
        return cell.DataType == XLDataType.Number && cell.TryGetValue<decimal>(out var value) && value > 0 ? value : throw CellError(row.Path, row.SheetName, row.RowNumber, column, "must be a positive numeric cell");
    }

    /// <summary>Reads a strictly numeric non-negative integer cell.</summary>
    /// <param name="row">Source row.</param><param name="column">One-based column number.</param><returns>Non-negative integer.</returns>
    private static int NonNegativeInt(RowContext row, int column)
    {
        var cell = row.Row.Cell(column);
        if (cell.DataType != XLDataType.Number || !cell.TryGetValue<decimal>(out var value) || value < 0 || value != decimal.Truncate(value) || value > int.MaxValue) throw CellError(row.Path, row.SheetName, row.RowNumber, column, "must be a non-negative integer numeric cell");
        return (int)value;
    }

    /// <summary>Reads a strictly boolean cell.</summary>
    /// <param name="row">Source row.</param><param name="column">One-based column number.</param><returns>Boolean value.</returns>
    private static bool Boolean(RowContext row, int column)
    {
        var cell = row.Row.Cell(column);
        return cell.DataType == XLDataType.Boolean && cell.TryGetValue<bool>(out var value) ? value : throw CellError(row.Path, row.SheetName, row.RowNumber, column, "must be a boolean cell");
    }

    /// <summary>Reads a strictly date-typed cell.</summary>
    /// <param name="row">Source row.</param><param name="column">One-based column number.</param><returns>Date-only value.</returns>
    private static DateOnly Date(RowContext row, int column)
    {
        var cell = row.Row.Cell(column);
        return cell.DataType == XLDataType.DateTime && cell.TryGetValue<DateTime>(out var value) ? DateOnly.FromDateTime(value) : throw CellError(row.Path, row.SheetName, row.RowNumber, column, "must be a date cell");
    }

    /// <summary>Reads a supported enum from a required text cell.</summary>
    /// <typeparam name="T">Enum type.</typeparam><param name="row">Source row.</param><param name="column">One-based column number.</param><returns>Parsed enum value.</returns>
    private static T EnumValue<T>(RowContext row, int column) where T : struct, Enum
        => Enum.TryParse<T>(Text(row, column), true, out var value) && Enum.IsDefined(value) ? value : throw CellError(row.Path, row.SheetName, row.RowNumber, column, "contains an unsupported value");

    /// <summary>Validates and trims a required configuration string.</summary>
    /// <param name="value">Configured value.</param><param name="name">Configuration key.</param><returns>Trimmed value.</returns>
    private static string RequiredText(string? value, string name) => string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"Configuration '{name}' is required.") : value.Trim();

    /// <summary>Validates a bounded integer configuration value.</summary>
    /// <param name="value">Configured value.</param><param name="minimum">Inclusive minimum.</param><param name="maximum">Inclusive maximum.</param><param name="name">Configuration key.</param><returns>The validated value.</returns>
    private static int InRange(int value, int minimum, int maximum, string name) => value < minimum || value > maximum ? throw new InvalidDataException($"Configuration '{name}' must be between {minimum} and {maximum}.") : value;

    /// <summary>Identifies the fixed master workbook.</summary>
    /// <param name="path">Workbook path.</param><returns>True for MasterData.xlsx.</returns>
    private static bool IsMaster(string path) => Path.GetFileName(path).Equals("MasterData.xlsx", StringComparison.OrdinalIgnoreCase);

    /// <summary>Creates a safe schema validation error.</summary>
    /// <param name="path">Workbook path.</param><param name="sheet">Sheet name.</param><param name="message">Safe detail.</param><returns>Validation exception.</returns>
    private static InvalidDataException SchemaError(string path, string sheet, string message) => new($"Workbook '{Path.GetFileName(path)}', sheet '{sheet}': {message}.");

    /// <summary>Creates a safe typed-cell validation error.</summary>
    /// <param name="path">Workbook path.</param><param name="sheet">Sheet name.</param><param name="row">One-based row.</param><param name="column">One-based column.</param><param name="message">Safe detail.</param><returns>Validation exception.</returns>
    private static InvalidDataException CellError(string path, string sheet, int row, int column, string message) => new($"Workbook '{Path.GetFileName(path)}', sheet '{sheet}', row {row}, column {column}: {message}.");

    /// <summary>Creates a source-aware reference error.</summary>
    /// <typeparam name="T">Row type.</typeparam><param name="row">Source row.</param><param name="type">Reference type.</param><param name="value">Unknown value.</param><returns>Validation exception.</returns>
    private static InvalidDataException ReferenceError<T>(ImportedRow<T> row, string type, string value) => new($"{Location(row)} references unknown {type} '{value}'.");

    /// <summary>Creates a source-aware domain validation error.</summary>
    /// <typeparam name="T">Row type.</typeparam><param name="row">Source row.</param><param name="message">Safe detail.</param><returns>Validation exception.</returns>
    private static InvalidDataException DomainError<T>(ImportedRow<T> row, string message) => new($"{Location(row)}: {message}.");

    /// <summary>Formats a workbook, sheet, and row location without exposing directories.</summary>
    /// <typeparam name="T">Row type.</typeparam><param name="row">Source row.</param><returns>Safe source location.</returns>
    private static string Location<T>(ImportedRow<T> row) => $"{Path.GetFileName(row.Path)}:{row.SheetName}:{row.RowNumber}";

    /// <summary>Defines an expected worksheet and exact ordered headers.</summary>
    /// <param name="Name">Worksheet name.</param><param name="Headers">Ordered headers.</param>
    private sealed record SheetSpec(string Name, string[] Headers);

    /// <summary>Owns one opened workbook and its source path.</summary>
    /// <param name="Path">Workbook path.</param><param name="Workbook">Opened workbook.</param>
    private sealed record WorkbookHandle(string Path, XLWorkbook Workbook);

    /// <summary>Provides typed-cell readers with source context.</summary>
    /// <param name="Path">Workbook path.</param><param name="SheetName">Worksheet name.</param><param name="Row">Worksheet row.</param><param name="RowNumber">One-based row number.</param>
    private sealed record RowContext(string Path, string SheetName, IXLRow Row, int RowNumber);

    /// <summary>Retains a parsed value and its workbook location.</summary>
    /// <typeparam name="T">Value type.</typeparam><param name="Value">Parsed value.</param><param name="Path">Workbook path.</param><param name="SheetName">Worksheet name.</param><param name="RowNumber">One-based row number.</param>
    private sealed record ImportedRow<T>(T Value, string Path, string SheetName, int RowNumber);

    /// <summary>Contains typed rows before uniqueness and reference indexing.</summary>
    /// <param name="Departments">Department rows.</param><param name="Employees">Employee rows.</param><param name="Vendors">Vendor rows.</param><param name="Categories">Category rows.</param><param name="Rates">Exchange-rate rows.</param><param name="Budgets">Budget rows.</param><param name="Policies">Policy rows.</param><param name="Transactions">Transaction rows.</param>
    private sealed record StagedData(List<ImportedRow<Department>> Departments, List<ImportedRow<Employee>> Employees, List<ImportedRow<Vendor>> Vendors, List<ImportedRow<ExpenseCategory>> Categories, List<ImportedRow<ExchangeRate>> Rates, List<ImportedRow<Budget>> Budgets, List<ImportedRow<PolicyRule>> Policies, List<ImportedRow<ExpenseTransaction>> Transactions);

    /// <summary>Contains duplicate-free lookups and source-aware list rows.</summary>
    /// <param name="Departments">Departments by identifier.</param><param name="Employees">Employees by identifier.</param><param name="Vendors">Vendors by identifier.</param><param name="Categories">Categories by identifier.</param><param name="Rates">Exchange-rate rows.</param><param name="Budgets">Budget rows.</param><param name="Policies">Policy rows.</param><param name="Transactions">Transaction rows.</param>
    private sealed record IndexedData(IReadOnlyDictionary<string, Department> Departments, IReadOnlyDictionary<string, Employee> Employees, IReadOnlyDictionary<string, Vendor> Vendors, IReadOnlyDictionary<string, ExpenseCategory> Categories, IReadOnlyList<ImportedRow<ExchangeRate>> Rates, IReadOnlyList<ImportedRow<Budget>> Budgets, IReadOnlyList<ImportedRow<PolicyRule>> Policies, IReadOnlyList<ImportedRow<ExpenseTransaction>> Transactions);
}
