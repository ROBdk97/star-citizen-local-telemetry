using System.Runtime.Versioning;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using StarCitizen.LocalTelemetry;
using StarCitizen.LocalTelemetry.Setup;

namespace TelemetryExplorer;

[SupportedOSPlatform("windows")]
internal sealed class MainWindow
{
    private const string AllSections = "(all sections)";
    private const string AllCategories = "(all categories)";

    private readonly StarCitizenTelemetryClient _client;
    private readonly Uri _address;
    private readonly TelemetryModel _model = new();
    private readonly CancellationTokenSource _stop = new();

    private readonly List<FieldRow> _fieldRows = [];
    private readonly List<GameAction> _actionRows = [];
    private readonly List<LogEntry> _logRows = [];
    private readonly ItemsView<FieldRow> _fieldsView;
    private readonly ItemsView<GameAction> _actionsView;
    private readonly ItemsView<LogEntry> _logView;

    private IReadOnlyList<GameAction> _actions = GameActions.Known;
    private string _actionSource = "built-in catalog";
    private string _section = AllSections;
    private string _fieldFilter = "";
    private string _actionFilter = "";
    private string _category = AllCategories;
    private GameAction? _selectedAction;
    private long _shownVersion = -1;
    private bool _triggering;

    private Window _window = null!;
    private TextBlock _status = null!;
    private TreeView _sectionTree = null!;
    private MultiLineTextBox _json = null!;
    private TextBlock _selectedText = null!;
    private NumericUpDown _hold = null!;
    private Button _triggerButton = null!;
    private ComboBox _categoryBox = null!;
    private IReadOnlyList<string> _shownSections = [];

    private StarCitizenInstallation? _installation;
    private GameChannel? _setupChannel;
    private TextBox _installPath = null!;
    private ComboBox _channelBox = null!;
    private TextBlock _setupStatus = null!;
    private TextBlock _setupState = null!;
    private TextBlock _setupResult = null!;
    private Button _enableButton = null!;
    private Button _disableButton = null!;

    public MainWindow(StarCitizenTelemetryClient client, Uri address)
    {
        _client = client;
        _address = address;
        _fieldsView = new ItemsView<FieldRow>(_fieldRows, r => r.Path, r => r.Path);
        _actionsView = new ItemsView<GameAction>(_actionRows, a => a.Name, a => a.Name);
        _logView = new ItemsView<LogEntry>(_logRows, e => e.Text, e => e);
    }

    public Window Build()
    {
        _status = new TextBlock().Text("Starting…");
        _window = new Window()
            .Title("Star Citizen Local Telemetry Explorer")
            .Resizable(1200, 800, minWidth: 800, minHeight: 500)
            .Content(new DockPanel().Spacing(6).Margin(8).Children(
                _status.DockTo(Dock.Bottom),
                new TabControl()
                    .Tab("Telemetry", BuildTelemetryTab())
                    .Tab("Actions", BuildActionsTab())
                    .Tab("Log", BuildLogTab())
                    .Tab("Raw JSON", BuildJsonTab())
                    .Tab("Setup", BuildSetupTab())));

        _window.Loaded += () =>
        {
            _ = Task.Run(StreamAsync);
            _ = LoadActionsAsync();
            _ = LocateGameAsync();
            new DispatcherTimer().IntervalMs(250).OnTick(Refresh).Start();
        };
        _window.Closed += () => _stop.Cancel();
        ApplyActionFilter();
        return _window;
    }

    private Element BuildTelemetryTab()
    {
        _sectionTree = new TreeView()
            .ItemsSource([new TreeViewNode(AllSections, [], AllSections)])
            .OnSelectedNodeChanged(node =>
            {
                _section = node?.Tag as string ?? AllSections;
                ApplyFieldFilter();
            });

        var grid = new GridView()
            .ZebraStriping()
            .Columns(
                new GridViewColumn<FieldRow>().Header("Field").Width(GridLength.Star, 220, double.PositiveInfinity)
                    .Text(r => _section == AllSections ? r.Path : r.Path[(r.Section.Length + 1)..]).SortBy(r => r.Path, StringComparer.Ordinal),
                new GridViewColumn<FieldRow>().Header("Value").Width(220).Text(r => r.Value),
                new GridViewColumn<FieldRow>().Header("Type").Width(130).Text(r => r.Type),
                new GridViewColumn<FieldRow>().Header("Changed").Width(110).Text(FormatAge).SortBy(r => r.LastChanged, null))
            .ItemsSource(_fieldsView);

        return new SplitPanel().Horizontal()
            .First(_sectionTree)
            .Second(new DockPanel().Spacing(6).Children(
                new TextBox().Placeholder("Filter fields or values…")
                    .OnTextChanged(text =>
                    {
                        _fieldFilter = text;
                        ApplyFieldFilter();
                    })
                    .DockTo(Dock.Top),
                grid))
            .FirstLength(new GridLength(200));
    }

    private Element BuildActionsTab()
    {
        _categoryBox = new ComboBox()
            .Width(240)
            .Items([AllCategories])
            .SelectedIndex(0)
            .OnSelectionChanged(item =>
            {
                _category = item as string ?? AllCategories;
                ApplyActionFilter();
            });

        _selectedText = new TextBlock().Text("Select an action.").Bold();
        _hold = new NumericUpDown().Minimum(0).Maximum(60_000).Value(0).IsInteger(true).Step(50).Width(120);
        _hold.IsEnabled = false;
        _triggerButton = new Button().Content("Trigger").OnClick(() => _ = TriggerSelectedAsync());
        _triggerButton.IsEnabled = false;

        var grid = new GridView()
            .ZebraStriping()
            .Columns(
                new GridViewColumn<GameAction>().Header("Action").Width(GridLength.Star, 260, double.PositiveInfinity)
                    .Text(a => a.Name).SortBy(a => a.Name, StringComparer.Ordinal),
                new GridViewColumn<GameAction>().Header("Category").Width(220)
                    .Text(a => a.Category).SortBy(a => a.Category, StringComparer.Ordinal),
                new GridViewColumn<GameAction>().Header("Holdable").Width(90).Text(a => a.Holdable ? "yes" : ""))
            .ItemsSource(_actionsView)
            .OnSelectionChanged(item => SelectAction(item as GameAction));
        grid.ItemDoubleClicked += item =>
        {
            SelectAction(item as GameAction);
            _ = TriggerSelectedAsync();
        };

        return new DockPanel().Spacing(6).Children(
            new StackPanel().Horizontal().Spacing(8).DockTo(Dock.Top).Children(
                new TextBox().Width(320).Placeholder("Search actions…").OnTextChanged(text =>
                {
                    _actionFilter = text;
                    ApplyActionFilter();
                }),
                _categoryBox),
            new StackPanel().Horizontal().Spacing(8).DockTo(Dock.Bottom).Children(
                _selectedText,
                new TextBlock().Text("Hold (ms):"),
                _hold,
                _triggerButton,
                new TextBlock().Text("Triggering sends the action to the game immediately.")),
            grid);
    }

    private Element BuildLogTab() =>
        new GridView()
            .ZebraStriping()
            .Columns(
                new GridViewColumn<LogEntry>().Header("Time").Width(110).Text(e => e.Time.ToLocalTime().ToString("HH:mm:ss.fff")),
                new GridViewColumn<LogEntry>().Header("Kind").Width(110).Text(e => e.Kind),
                new GridViewColumn<LogEntry>().Header("Details").Width(GridLength.Star, 300, double.PositiveInfinity).Text(e => e.Text))
            .ItemsSource(_logView);

    private Element BuildJsonTab()
    {
        _json = new MultiLineTextBox().IsReadOnly(true).FontFamily("Consolas");
        return new DockPanel().Spacing(6).Children(
            new Button().Content("Refresh").OnClick(() => _json.Text = _model.GetSnapshot().Json).DockTo(Dock.Top),
            _json);
    }

    private Element BuildSetupTab()
    {
        _installPath = new TextBox().Width(420).Placeholder(@"Install folder, e.g. C:\Program Files\Roberts Space Industries\StarCitizen");
        _channelBox = new ComboBox().Width(160).OnSelectionChanged(item =>
        {
            _setupChannel = _installation?.FindChannel(item as string ?? "");
            ShowSetupStatus();
        });
        _setupStatus = new TextBlock().Text("Looking for Star Citizen…");
        _setupState = new TextBlock().Text("Configured telemetry: unknown").Bold();
        _setupResult = new TextBlock().Text("");
        _enableButton = new Button().Content("Enable telemetry").OnClick(() => ApplySetup(enable: true));
        _disableButton = new Button().Content("Disable telemetry").OnClick(() => ApplySetup(enable: false));
        _enableButton.IsEnabled = _disableButton.IsEnabled = false;

        return new StackPanel().Spacing(10).Children(
            new StackPanel().Horizontal().Spacing(8).Children(
                new TextBlock().Text("Installation:"),
                _installPath,
                new Button().Content("Use folder").OnClick(UseInstallPath),
                new Button().Content("Find automatically").OnClick(() => _ = LocateGameAsync())),
            new StackPanel().Horizontal().Spacing(8).Children(
                new TextBlock().Text("Channel:"),
                _channelBox,
                _enableButton,
                _disableButton,
                new Button().Content("Refresh").OnClick(ShowSetupStatus)),
            _setupState,
            _setupStatus,
            _setupResult,
            new TextBlock().Text("This shows the saved configuration, not the current in-game console setting."),
            new TextBlock().Text(
                $"Enable sets the user environment variable {TelemetrySetup.EnvironmentVariable}=true and " +
                $"{TelemetrySetup.ConsoleVariable} = 1 in the channel's user.cfg (backed up first). " +
                "Disable sets the console variable to 0. Changes apply after restarting the RSI Launcher and the game."));
    }

    private async Task LocateGameAsync()
    {
        _setupState.Text = "Configured telemetry: unknown";
        _setupStatus.Text = "Looking for Star Citizen…";
        try
        {
            ShowInstallation(await StarCitizenLocator.FindAsync(_stop.Token));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _setupStatus.Text = $"Search failed: {ex.Message}";
        }
    }

    private void UseInstallPath()
    {
        var path = _installPath.Text.Trim();
        if (path.Length == 0) return;
        ShowInstallation(StarCitizenLocator.FromPath(path));
    }

    private void ShowInstallation(StarCitizenInstallation? installation)
    {
        _installation = installation;
        _setupResult.Text = "";
        if (installation is null || installation.Channels.Count == 0)
        {
            _setupChannel = null;
            _channelBox.Items([]);
            _setupState.Text = "Configured telemetry: unknown";
            _setupStatus.Text = "Star Citizen was not found. Enter the install folder (the one containing LIVE or PTU).";
            _enableButton.IsEnabled = _disableButton.IsEnabled = false;
            return;
        }

        _installPath.Text = installation.RootPath;
        var names = installation.Channels.Select(c => c.Name).ToArray();
        var preferred = Array.FindIndex(names, n => n.Equals("PTU", StringComparison.OrdinalIgnoreCase));
        _channelBox.Items(names).SelectedIndex(preferred < 0 ? 0 : preferred);
        _setupChannel = installation.Channels[preferred < 0 ? 0 : preferred];
        ShowSetupStatus();
    }

    private void ShowSetupStatus()
    {
        _enableButton.IsEnabled = _disableButton.IsEnabled = _setupChannel is not null;
        if (_setupChannel is not { } channel)
        {
            _setupState.Text = "Configured telemetry: unknown";
            return;
        }
        try
        {
            var status = TelemetrySetup.GetStatus(channel);
            _setupState.Text = SetupStatusText.DescribeState(status);
            _setupStatus.Text = SetupStatusText.Describe(status, TelemetrySchema.CheckGameVersion(channel));
        }
        catch (Exception ex)
        {
            _setupState.Text = "Configured telemetry: unknown";
            _setupStatus.Text = $"Could not read the configuration: {ex.Message}";
        }
    }

    private void ApplySetup(bool enable)
    {
        if (_setupChannel is not { } channel) return;
        var action = enable ? "Enable" : "Disable";
        try
        {
            var result = enable ? TelemetrySetup.Enable(channel) : TelemetrySetup.Disable(channel);
            _setupResult.Text = SetupStatusText.Describe(action, result);
            _model.LogAction($"Setup {channel.Name}", null, null, _setupResult.Text);
        }
        catch (Exception ex)
        {
            _setupResult.Text = $"{action} failed: {ex.Message}";
        }

        ShowSetupStatus();
    }

    private async Task StreamAsync()
    {
        try
        {
            await foreach (var streamEvent in _client.ObserveTelemetryAsync(cancellationToken: _stop.Token))
                _model.Apply(streamEvent);
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _model.SetStatus($"Stream stopped: {ex.Message}");
        }
    }

    private async Task LoadActionsAsync()
    {
        try
        {
            _actions = await _client.GetActionsAsync(_stop.Token);
            _actionSource = "from game";
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _model.LogAction("ListTriggerableActions", null, null, $"failed, using built-in catalog: {ex.Message}");
        }

        string[] categories = [AllCategories, .. _actions.Select(a => a.Category).Distinct().Order(StringComparer.Ordinal)];
        _categoryBox.Items(categories).SelectedIndex(0);
        _category = AllCategories;
        ApplyActionFilter();
    }

    private void Refresh()
    {
        var snapshot = _model.GetSnapshot();
        var age = snapshot.LastFrameAt is { } at ? $"{(DateTimeOffset.UtcNow - at).TotalSeconds:0.0}s ago" : "never";
        _status.Text = $"{_address}  •  {snapshot.Status}  •  {snapshot.FrameCount} frames, last {age}  •  " +
                       $"{_actions.Count} actions ({_actionSource})";
        if (snapshot.Version == _shownVersion) return;
        _shownVersion = snapshot.Version;

        if (!snapshot.Sections.SequenceEqual(_shownSections))
        {
            _shownSections = snapshot.Sections;
            _sectionTree.ItemsSource(
                [new TreeViewNode(AllSections, [], AllSections), .. snapshot.Sections.Select(s => new TreeViewNode(s, [], s))]);
        }

        ApplyFieldFilter(snapshot);
        _logRows.Clear();
        _logRows.AddRange(snapshot.Log);
        _logView.Invalidate();
        if (string.IsNullOrEmpty(_json.Text)) _json.Text = snapshot.Json;
    }

    private void ApplyFieldFilter() => ApplyFieldFilter(_model.GetSnapshot());

    private void ApplyFieldFilter(ModelSnapshot snapshot)
    {
        _fieldRows.Clear();
        _fieldRows.AddRange(snapshot.Fields.Where(r =>
            (_section == AllSections || r.Section == _section) &&
            (_fieldFilter.Length == 0 ||
             r.Path.Contains(_fieldFilter, StringComparison.OrdinalIgnoreCase) ||
             r.Value.Contains(_fieldFilter, StringComparison.OrdinalIgnoreCase))));
        _fieldsView.Invalidate();
    }

    private void ApplyActionFilter()
    {
        _actionRows.Clear();
        _actionRows.AddRange(_actions.Where(a =>
            (_category == AllCategories || a.Category == _category) &&
            (_actionFilter.Length == 0 ||
             a.Name.Contains(_actionFilter, StringComparison.OrdinalIgnoreCase) ||
             a.Category.Contains(_actionFilter, StringComparison.OrdinalIgnoreCase))));
        _actionsView.Invalidate();
    }

    private void SelectAction(GameAction? action)
    {
        _selectedAction = action;
        _selectedText.Text = action?.Name ?? "Select an action.";
        _hold.IsEnabled = action?.Holdable == true;
        _triggerButton.IsEnabled = action is not null && !_triggering;
    }

    private async Task TriggerSelectedAsync()
    {
        if (_selectedAction is not { } action || _triggering) return;
        TimeSpan? hold = action.Holdable && _hold.Value > 0 ? TimeSpan.FromMilliseconds(_hold.Value) : null;
        _triggering = true;
        _triggerButton.IsEnabled = false;
        try
        {
            var result = await _client.TriggerActionAsync(action.Name, hold, _stop.Token);
            _model.LogAction(action.Name, hold, result);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _model.LogAction(action.Name, hold, null, $"error: {ex.Message}");
        }
        finally
        {
            _triggering = false;
            _triggerButton.IsEnabled = _selectedAction is not null;
        }
    }

    private static string FormatAge(FieldRow row)
    {
        var seconds = (DateTimeOffset.UtcNow - row.LastChanged).TotalSeconds;
        return seconds < 2 ? "● now" : seconds < 60 ? $"{seconds:0}s ago" : row.LastChanged.ToLocalTime().ToString("HH:mm:ss");
    }
}
