using System.Globalization;
using System.Text.Json;

namespace CalculatorWinForms;

public sealed class CalculatorForm : Form
{
    private readonly TextBox display = new();
    private readonly Label status = new();
    private readonly ListBox historyList = new();
    private readonly List<HistoryEntry> history = new();
    private double accumulator;
    private string? pendingOperator;
    private bool startNewNumber = true;
    private bool darkTheme;
    private bool errorState;

    public CalculatorForm()
    {
        Text = "Калькулятор — Math Games";
        MinimumSize = new Size(390, 590);
        Size = new Size(430, 680);
        StartPosition = FormStartPosition.CenterScreen;
        BuildInterface();
        ApplyTheme();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35));
        Controls.Add(root);

        display.Dock = DockStyle.Fill;
        display.ReadOnly = true;
        display.TextAlign = HorizontalAlignment.Right;
        display.Font = new Font("Segoe UI", 25, FontStyle.Bold);
        display.Text = "0";
        display.BorderStyle = BorderStyle.FixedSingle;
        root.Controls.Add(display, 0, 0);

        status.Dock = DockStyle.Fill;
        status.TextAlign = ContentAlignment.MiddleRight;
        status.Text = "Готово";
        root.Controls.Add(status, 0, 1);

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 5, Padding = new Padding(0, 6, 0, 6) };
        for (int i = 0; i < 4; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        for (int i = 0; i < 5; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 20));
        root.Controls.Add(grid, 0, 2);

        string[,] keys = {
            { "C", "±", "⌫", "÷" },
            { "7", "8", "9", "×" },
            { "4", "5", "6", "−" },
            { "1", "2", "3", "+" },
            { "0", ".", "=", "Тема" }
        };
        for (int r = 0; r < 5; r++)
        for (int c = 0; c < 4; c++)
        {
            string key = keys[r, c];
            var button = new Button
            {
                Text = key,
                Dock = DockStyle.Fill,
                Margin = new Padding(4),
                Font = new Font("Segoe UI", key == "Тема" ? 10 : 17, FontStyle.Bold),
                FlatStyle = FlatStyle.Flat,
                Tag = key
            };
            button.Click += KeyButton_Click;
            grid.Controls.Add(button, c, r);
        }

        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(0, 8, 0, 0) };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var saveButton = new Button { Text = "Сохранить историю", Dock = DockStyle.Fill, Margin = new Padding(3) };
        var loadButton = new Button { Text = "Загрузить историю", Dock = DockStyle.Fill, Margin = new Padding(3) };
        saveButton.Click += (_, _) => SaveHistory();
        loadButton.Click += (_, _) => LoadHistory();
        bottom.Controls.Add(saveButton, 0, 0);
        bottom.Controls.Add(loadButton, 1, 0);
        historyList.Dock = DockStyle.Fill;
        historyList.Font = new Font("Segoe UI", 9);
        bottom.Controls.Add(historyList, 0, 1);
        bottom.SetColumnSpan(historyList, 2);
        root.Controls.Add(bottom, 0, 3);
    }

    private void KeyButton_Click(object? sender, EventArgs e)
    {
        if (sender is not Button button || button.Tag is not string key) return;
        if (key.Length == 1 && char.IsDigit(key[0])) { InputDigit(key); return; }
        switch (key)
        {
            case ".": InputDecimal(); break;
            case "C": Clear(); break;
            case "⌫": Backspace(); break;
            case "±": ToggleSign(); break;
            case "+": case "−": case "×": case "÷": SetOperator(key); break;
            case "=": Calculate(); break;
            case "Тема": darkTheme = !darkTheme; ApplyTheme(); break;
        }
    }

    private void InputDigit(string digit)
    {
        if (errorState) Clear();
        if (startNewNumber || display.Text == "0")
        {
            display.Text = digit;
            startNewNumber = false;
        }
        else if (display.Text.Length < 18) display.Text += digit;
    }

    private void InputDecimal()
    {
        if (errorState) Clear();
        if (startNewNumber) { display.Text = "0."; startNewNumber = false; }
        else if (!display.Text.Contains('.')) display.Text += ".";
    }

    private void SetOperator(string op)
    {
        if (!TryReadDisplay(out double current)) return;
        if (pendingOperator is not null && !startNewNumber)
        {
            if (!ApplyPending(current, out double result)) return;
            accumulator = result;
            display.Text = Format(result);
        }
        else accumulator = current;
        pendingOperator = op;
        startNewNumber = true;
        status.Text = $"{Format(accumulator)} {op}";
    }

    private void Calculate()
    {
        if (pendingOperator is null || !TryReadDisplay(out double current)) return;
        string expression = $"{Format(accumulator)} {pendingOperator} {Format(current)}";
        if (!ApplyPending(current, out double result)) return;
        display.Text = Format(result);
        AddHistory($"{expression} = {Format(result)}");
        accumulator = result;
        pendingOperator = null;
        startNewNumber = true;
        status.Text = "Результат";
    }

    private bool ApplyPending(double right, out double result)
    {
        result = 0;
        switch (pendingOperator)
        {
            case "+": result = accumulator + right; break;
            case "−": result = accumulator - right; break;
            case "×": result = accumulator * right; break;
            case "÷":
                if (right == 0) { ShowError("Деление на ноль невозможно."); return false; }
                result = accumulator / right;
                break;
            default: return false;
        }
        if (double.IsNaN(result) || double.IsInfinity(result)) { ShowError("Результат выходит за допустимый диапазон."); return false; }
        return true;
    }

    private bool TryReadDisplay(out double value)
    {
        if (double.TryParse(display.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) return true;
        ShowError("Некорректное число.");
        return false;
    }

    private void ToggleSign()
    {
        if (errorState) return;
        if (display.Text == "0") return;
        display.Text = display.Text.StartsWith('-') ? display.Text[1..] : "-" + display.Text;
    }

    private void Backspace()
    {
        if (errorState) { Clear(); return; }
        if (startNewNumber) return;
        display.Text = display.Text.Length <= 1 || (display.Text.Length == 2 && display.Text.StartsWith('-'))
            ? "0" : display.Text[..^1];
    }

    private void Clear()
    {
        display.Text = "0";
        accumulator = 0;
        pendingOperator = null;
        startNewNumber = true;
        errorState = false;
        status.Text = "Готово";
    }

    private void ShowError(string message)
    {
        display.Text = "Ошибка";
        status.Text = message;
        pendingOperator = null;
        startNewNumber = true;
        errorState = true;
        MessageBox.Show(message, "Ошибка вычисления", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    private static string Format(double value) => value.ToString("G12", CultureInfo.InvariantCulture);

    private void AddHistory(string expression)
    {
        history.Add(new HistoryEntry(DateTime.Now, expression));
        historyList.Items.Insert(0, $"{DateTime.Now:dd.MM.yyyy HH:mm:ss}  {expression}");
    }

    private void SaveHistory()
    {
        using var dialog = new SaveFileDialog { Filter = "Файл истории JSON (*.json)|*.json", FileName = "calculator-history.json" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true }));
            MessageBox.Show("История успешно сохранена.", "Сохранение", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось сохранить историю: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void LoadHistory()
    {
        using var dialog = new OpenFileDialog { Filter = "Файл истории JSON (*.json)|*.json" };
        if (dialog.ShowDialog() != DialogResult.OK) return;
        try
        {
            var loaded = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(dialog.FileName)) ?? new();
            history.Clear(); history.AddRange(loaded);
            historyList.Items.Clear();
            foreach (var item in history.AsEnumerable().Reverse()) historyList.Items.Insert(0, $"{item.Timestamp:dd.MM.yyyy HH:mm:ss}  {item.Expression}");
            MessageBox.Show("История успешно загружена.", "Загрузка", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex) { MessageBox.Show($"Не удалось загрузить историю: {ex.Message}", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }

    private void ApplyTheme()
    {
        BackColor = darkTheme ? Color.FromArgb(32, 33, 36) : Color.FromArgb(245, 246, 248);
        ForeColor = darkTheme ? Color.White : Color.FromArgb(30, 30, 30);
        display.BackColor = darkTheme ? Color.FromArgb(48, 49, 52) : Color.White;
        display.ForeColor = ForeColor;
        status.ForeColor = ForeColor;
        historyList.BackColor = darkTheme ? Color.FromArgb(48, 49, 52) : Color.White;
        historyList.ForeColor = ForeColor;
        foreach (Control control in Controls)
            ApplyButtonColors(control);
    }

    private void ApplyButtonColors(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is Button button)
            {
                button.BackColor = darkTheme ? Color.FromArgb(65, 67, 72) : Color.White;
                button.ForeColor = darkTheme ? Color.White : Color.FromArgb(30, 30, 30);
                button.FlatAppearance.BorderColor = darkTheme ? Color.FromArgb(90, 92, 98) : Color.LightGray;
            }
            if (child.HasChildren) ApplyButtonColors(child);
        }
    }

    private sealed record HistoryEntry(DateTime Timestamp, string Expression);
}
