using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DataStructurePlugin.Features.AhoCorasick.Application;
using DataStructurePlugin.Features.AhoCorasick.Content;
using DataStructurePlugin.Features.AhoCorasick.Domain;
using MyAvaloniaManagement.PluginSdk;

namespace DataStructurePlugin.Features.AhoCorasick;

/// <summary>
/// AC 课堂的单实例页面协调器：拥有草稿、阅读选择、练习反馈，并把用户命令交给算法或播放会话。
/// 算法由 IAcTraceBuilder 注入，计时由 AcPlaybackSession 管理，检查器文字由纯投影生成。
/// 设计上保持一个域一个 Document，不通过通用基类或算法类型分支混入未来专题。
/// </summary>
public sealed partial class AhoCorasickDocument : ObservableObject, IPluginDocument, IDisposable
{
    private readonly IAcTraceBuilder _traceBuilder;
    private readonly AcPlaybackSession _playback;
    private readonly ObservableCollection<AcMatchRow> _visibleMatches = [];
    private ImmutableArray<AcMatchRow> _matchRows;
    private AcInspection _inspection;
    private DocumentPresentationState _presentation = new("AC 自动机 · 交互课堂");
    private AcLesson _selectedLesson = LessonCatalog.All[0];
    private string _builtKeywords;
    private int _selectedNodeId;
    private bool _projecting;
    private bool _disposed;

    public AhoCorasickDocument(IAcTraceBuilder traceBuilder, IAcPlaybackClock clock)
    {
        _traceBuilder = traceBuilder ?? throw new ArgumentNullException(nameof(traceBuilder));
        _playback = new(clock);
        VisibleMatches = new(_visibleMatches);
        // 第一个实验必须完整存在，才能向绑定暴露 Document。依赖失败时释放已取得的时钟后明确失败。
        try
        {
            var trace = _traceBuilder.Build(KeywordText, SearchText);
            _matchRows = AcTraceProjection.CreateMatchRows(trace);
            _playback.Load(trace);
            _builtKeywords = KeywordText;
            _inspection = AcTraceProjection.Inspect(trace, CurrentStep, 0);
            _playback.Changed += OnPlaybackChanged;
        }
        catch
        {
            _playback.Dispose();
            throw;
        }
    }

    public DocumentPresentationState Presentation => _presentation;
    public event EventHandler? PresentationChanged;
    public IReadOnlyList<AcLesson> Lessons => LessonCatalog.All;
    public IReadOnlyList<ExampleCase> Examples => AcExampleCatalog.All;
    public IReadOnlyList<double> SpeedOptions { get; } = ImmutableArray.Create(0.5, 1, 2, 4);
    public ReadOnlyObservableCollection<AcMatchRow> VisibleMatches { get; }
    public AcTrace Trace => _playback.Trace;
    public AcStep CurrentStep => _playback.CurrentStep;
    public int FrameIndex => _playback.FrameIndex;
    public int MaximumPosition => _playback.MaximumPosition;
    public bool IsPlaying => _playback.IsPlaying;
    public double Position { get => _playback.Position; set => _playback.Position = value; }
    public double Speed { get => _playback.Speed; set => _playback.Speed = value; }

    [ObservableProperty] private string _keywordText = "he\nshe\nhis\nhers";
    [ObservableProperty] private string _searchText = "ushers";
    [ObservableProperty] private bool _isReading = true;
    [ObservableProperty] private bool _showFailures = true;
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _feedback = "先做一个预测，再展开解释。";
    [ObservableProperty] private int _answerIndex = -1;
    [ObservableProperty] private ExampleCase? _selectedExample;
    [ObservableProperty] private AcMatchRow? _selectedMatch;

    public AcLesson SelectedLesson
    {
        get => _selectedLesson;
        set
        {
            // 列表重排或切换响应式布局时可能短暂取消选择；教材保持上一章，不向绑定传播空章节。
            if (_disposed || value is null || !SetProperty(ref _selectedLesson, value)) return;
            Pause();
            AnswerIndex = -1;
            Feedback = "先做一个预测，再展开解释。";
        }
    }

    public int SelectedNodeId
    {
        get => _selectedNodeId;
        set
        {
            if (_disposed) return;
            var normalized = Math.Clamp(value, 0, CurrentStep.VisibleNodes - 1);
            if (SetProperty(ref _selectedNodeId, normalized)) UpdateInspection();
        }
    }

    public bool HasError => ErrorMessage.Length > 0;
    public bool IsDraft => KeywordText != _builtKeywords || SearchText != Trace.Text;
    public string PlayLabel => IsPlaying ? "Ⅱ  暂停" : "▶  播放";
    public string ModeCaption => IsReading ? "跟着问题理解，再亲手验证。" : "换一组输入，观察同一套规则如何工作。";
    public string ProgressLabel => $"{FrameIndex + 1} / {Trace.Steps.Length}";
    public string StageLabel => CurrentStep.Stage switch { AcStage.Trie => "01  构建 Trie", AcStage.Failure => "02  构建 fail", _ => "03  扫描文本" };
    public string Statistics => $"{Trace.Patterns.Length} 个关键词   ·   {Trace.Nodes.Length} 个状态   ·   {Trace.Symbols.Length} 个文本字符";
    public string CursorLabel => $"已消费 {CurrentStep.ConsumedSymbols} / {Trace.Symbols.Length} 个码点";
    public string QueueLabel => CurrentStep.Queue.IsEmpty ? "队列为空" : string.Join("  →  ", CurrentStep.Queue.Select(DisplayNode));
    public string MatchSummary => $"已发现 {CurrentStep.MatchCount} 次命中";
    public string NextEventLabel => CurrentStep.Stage == AcStage.Search ? "下一次命中 →" : "下一个关键步骤 →";
    public string InspectedPrefix => _inspection.Prefix;
    public string InspectedFailure => _inspection.Failure;
    public string OwnOutput => _inspection.OwnOutput;
    public string InheritedOutput => _inspection.InheritedOutput;
    public string SuffixExplanation => _inspection.SuffixExplanation;

    public ValueTask InitializeAsync(DocumentActivation activation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activation);
        cancellationToken.ThrowIfCancellationRequested();
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!string.IsNullOrWhiteSpace(activation.Title))
        {
            _presentation = new(activation.Title);
            PresentationChanged?.Invoke(this, EventArgs.Empty);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 先完成纯算法计算和结果索引，成功后才统一提交。失败保留旧实验，只有错误文本和草稿提示变化。
    /// 提交期间抑制列表的选择回写，避免旧结果指向新轨迹的同一编号步骤。
    /// </summary>
    [RelayCommand]
    private void Build()
    {
        if (_disposed) return;
        Pause();
        try
        {
            var trace = _traceBuilder.Build(KeywordText, SearchText);
            var rows = AcTraceProjection.CreateMatchRows(trace);
            _projecting = true;
            try
            {
                SelectedMatch = null;
                _matchRows = rows;
                _playback.Load(trace);
                _builtKeywords = KeywordText;
                ErrorMessage = "";
            }
            finally { _projecting = false; }
            RefreshFrame();
            OnPropertyChanged(nameof(Trace));
            OnPropertyChanged(nameof(Statistics));
        }
        catch (ArgumentException exception) { ErrorMessage = exception.Message; }
        OnPropertyChanged(nameof(IsDraft));
    }

    [RelayCommand] private void Previous() => Seek(FrameIndex - 1);
    [RelayCommand] private void Next() => Seek(FrameIndex + 1);
    [RelayCommand] private void Restart() => Seek(0);
    [RelayCommand] private void Finish() => Seek(MaximumPosition);
    [RelayCommand] private void GoTrie() => Seek(Trace.StageStart(AcStage.Trie));
    [RelayCommand] private void GoFailure() => Seek(Trace.StageStart(AcStage.Failure));
    [RelayCommand] private void GoSearch() => Seek(Trace.StageStart(AcStage.Search));
    [RelayCommand] private void ReadMode() { if (!_disposed) IsReading = true; }
    [RelayCommand] private void ExploreMode() { if (!_disposed) IsReading = false; }
    [RelayCommand] private void TogglePlayback() => _playback.TogglePlayback();
    public void Pause() => _playback.Pause();
    public void Seek(int index) => _playback.Seek(index);

    [RelayCommand]
    private void NextEvent()
    {
        if (_disposed) return;
        var stage = CurrentStep.Stage;
        var index = Trace.FindStep(FrameIndex + 1, step => stage == AcStage.Search
            ? step.Action is AcAction.Match or AcAction.Complete
            : step.Action is AcAction.Terminal or AcAction.SetFailure || step.Stage != stage);
        Seek(index < 0 ? MaximumPosition : index);
    }

    [RelayCommand]
    private void ApplyExample()
    {
        if (_disposed) return;
        var example = SelectedExample ?? Examples[0];
        KeywordText = example.Keywords;
        SearchText = example.Text;
        Build();
    }

    [RelayCommand]
    private void DemonstrateLesson()
    {
        if (_disposed) return;
        SelectedExample = Examples[0];
        ApplyExample();
        Seek(SelectedLesson.FindDemoStep(Trace));
    }

    [RelayCommand]
    private void CheckAnswer()
    {
        if (_disposed) return;
        Feedback = AnswerIndex < 0 || AnswerIndex >= SelectedLesson.Answers.Length ? "先选择一个答案，再检查你的理解。"
            : (AnswerIndex == SelectedLesson.CorrectAnswer ? "✓  判断正确。" : "再想一步。") + " " + SelectedLesson.Feedback;
    }

    private string DisplayNode(int id) => id == 0 ? "ε" : Trace.Nodes[id].Prefix;
    partial void OnKeywordTextChanged(string value) { Pause(); OnPropertyChanged(nameof(IsDraft)); }
    partial void OnSearchTextChanged(string value) { Pause(); OnPropertyChanged(nameof(IsDraft)); }
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnIsReadingChanged(bool value) { Pause(); OnPropertyChanged(nameof(ModeCaption)); }

    partial void OnSelectedMatchChanged(AcMatchRow? value)
    {
        // 只有当前实验中仍然可见的同一行对象才有导航权；迟到的旧绑定通知不应改写游标。
        if (_disposed || _projecting || value is null) return;
        if (_visibleMatches.Any(row => ReferenceEquals(row, value))) Seek(value.StepIndex);
    }

    private void OnPlaybackChanged(object? sender, EventArgs args)
    {
        if (!_disposed && !_projecting) RefreshFrame();
    }

    private void RefreshFrame()
    {
        _projecting = true;
        try
        {
            SelectedNodeId = CurrentStep.State;
            UpdateInspection();
            // 集合身份保持稳定，只更新已发现的前缀，避免每一帧重建 ListBox 选择状态。
            while (_visibleMatches.Count > CurrentStep.MatchCount) _visibleMatches.RemoveAt(_visibleMatches.Count - 1);
            while (_visibleMatches.Count < CurrentStep.MatchCount) _visibleMatches.Add(_matchRows[_visibleMatches.Count]);
            foreach (var property in new[] { nameof(CurrentStep), nameof(Position), nameof(FrameIndex), nameof(MaximumPosition),
                nameof(IsPlaying), nameof(PlayLabel), nameof(Speed), nameof(ProgressLabel), nameof(StageLabel),
                nameof(CursorLabel), nameof(QueueLabel), nameof(MatchSummary), nameof(NextEventLabel) })
                OnPropertyChanged(property);
        }
        finally { _projecting = false; }
    }

    private void UpdateInspection()
    {
        _inspection = AcTraceProjection.Inspect(Trace, CurrentStep, SelectedNodeId);
        foreach (var property in new[] { nameof(InspectedPrefix), nameof(InspectedFailure), nameof(OwnOutput), nameof(InheritedOutput), nameof(SuffixExplanation) })
            OnPropertyChanged(property);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _playback.Changed -= OnPlaybackChanged;
        _playback.Dispose();
    }
}
