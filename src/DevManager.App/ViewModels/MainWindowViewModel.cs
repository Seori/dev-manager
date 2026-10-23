using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevManager.Core.Entities;
using DevManager.Data;
using Microsoft.Extensions.Configuration;

namespace DevManager.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AppService _svc;
    private User? _me;
    private bool _suppress;
    private int _detailId;
    private int _detailVersion;
    private int? _editId;
    private int _editVersion;

    // csproj의 <Version>을 그대로 표시한다.
    public string StatusText { get; } =
        $"DB 연결됨 (버전 : {System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "1.0.0"})";

    public ObservableCollection<RequirementRow> Items { get; } = [];
    public ObservableCollection<Option> JobTitles { get; } = [];
    public ObservableCollection<Option> Categories { get; } = [];
    public ObservableCollection<Option> AssignOptions { get; } = [];
    public ObservableCollection<Option> UserOptions { get; } = [];

    [ObservableProperty] private bool _isSetup;
    [ObservableProperty] private bool _isLogin;
    [ObservableProperty] private bool _isMain;
    [ObservableProperty] private string _error = "";

    [ObservableProperty] private string _setupName = "";
    [ObservableProperty] private string _setupLoginId = "";
    [ObservableProperty] private string _setupPassword = "";
    [ObservableProperty] private string _setupConfirm = "";
    [ObservableProperty] private Option? _setupJobTitle;

    [ObservableProperty] private string _loginId = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _saveLoginId;

    [ObservableProperty] private string _displayName = "";
    [ObservableProperty] private string _jobTitleName = "";
    [ObservableProperty] private string _initial = "";
    [ObservableProperty] private bool _isAdmin;
    [ObservableProperty] private bool _showAll;
    [ObservableProperty] private int _unreadCount;
    [ObservableProperty] private bool _hasUnread;
    [ObservableProperty] private string _listHeader = "";
    [ObservableProperty] private RequirementRow? _selectedItem;

    [ObservableProperty] private bool _showDetail;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private string _detailDocNo = "";
    [ObservableProperty] private string _detailTitle = "";
    [ObservableProperty] private string _detailDesc = "";
    [ObservableProperty] private string _detailCategory = "";
    [ObservableProperty] private string _detailRequester = "";
    [ObservableProperty] private string _detailDue = "";
    [ObservableProperty] private string _detailStart = "";
    [ObservableProperty] private string _detailEnd = "";
    [ObservableProperty] private string _detailDeploy = "";
    [ObservableProperty] private string _detailAssignee = "";
    [ObservableProperty] private Option? _selectedAssignee;
    [ObservableProperty] private string _detailMessage = "";
    [ObservableProperty] private string _detailStatus = "";
    [ObservableProperty] private string _detailCompletedModules = "";
    [ObservableProperty] private string _detailCompletedAt = "";
    [ObservableProperty] private bool _showCompletedInfo;
    [ObservableProperty] private bool _canComplete;
    [ObservableProperty] private bool _showComplete;
    [ObservableProperty] private string _completeModules = "";
    [ObservableProperty] private string _completeError = "";

    [ObservableProperty] private bool _showForm;
    [ObservableProperty] private string _formHeader = "";
    [ObservableProperty] private string _formTitle = "";
    [ObservableProperty] private string _formDesc = "";
    [ObservableProperty] private Option? _formRequester;
    [ObservableProperty] private DateTime? _formDue;
    [ObservableProperty] private DateTime? _formStart;
    [ObservableProperty] private DateTime? _formEnd;
    [ObservableProperty] private DateTime? _formDeploy;
    [ObservableProperty] private Option? _formCategory;
    [ObservableProperty] private string _formError = "";
    [ObservableProperty] private bool _canEditDevDates;
    [ObservableProperty] private bool _canEditCore = true;

    [ObservableProperty] private bool _showUserForm;
    [ObservableProperty] private string _newUserLoginId = "";
    [ObservableProperty] private string _newUserName = "";
    [ObservableProperty] private string _newUserPassword = "";
    [ObservableProperty] private Option? _newUserJobTitle;
    [ObservableProperty] private string _userFormMessage = "";

    public MainWindowViewModel()
    {
        try
        {
            var cfg = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false).Build();
            var settings = new DatabaseSettings
            {
                Path = cfg["Database:Path"] ?? "data/devmanager.db",
                BusyTimeoutSeconds = int.TryParse(cfg["Database:BusyTimeoutSeconds"], out var t) ? t : 5
            };
            _svc = new AppService(settings, AppContext.BaseDirectory);
            _svc.Initialize();

            var seed = Path.Combine(AppContext.BaseDirectory, "seed.local.json");
            if (File.Exists(seed))
            {
                _svc.SeedAccounts(File.ReadAllText(seed));
                File.Delete(seed);
            }

            foreach (var o in _svc.JobTitles()) JobTitles.Add(o);
            foreach (var o in _svc.Categories()) Categories.Add(o);
            LoadSavedLoginId();
            if (_svc.HasAdmin()) IsLogin = true; else IsSetup = true;
        }
        catch (Exception ex)
        {
            _svc = null!;
            IsLogin = true;
            Error = "DB를 열 수 없습니다: " + ex.Message;
        }
    }

    [RelayCommand]
    private void CreateAdmin()
    {
        var err = _svc.CreateAdmin(SetupLoginId, SetupName, SetupJobTitle?.Id, SetupPassword, SetupConfirm);
        if (err != null) { Error = err; return; }
        Error = "";
        LoginId = SetupLoginId;
        SetupPassword = SetupConfirm = "";
        IsSetup = false;
        IsLogin = true;
    }

    [RelayCommand]
    private void DoLogin()
    {
        if (_svc == null) return;
        var (user, err) = _svc.Login(LoginId, Password);
        if (user == null) { Error = err ?? ""; return; }
        Error = "";
        Password = "";
        StoreLoginId(user.LoginId);
        _me = user;
        DisplayName = user.Name;
        JobTitleName = user.JobTitle?.Name ?? "";
        Initial = user.Name.Length > 0 ? user.Name[..1] : "?";
        IsAdmin = user.Role == GlobalRole.Admin;
        ShowAll = IsAdmin;
        IsLogin = false;
        IsMain = true;
        Reload();
        _timer ??= StartTimer();
    }

    // 아이디만 이 PC의 로컬 설정에 저장한다(비밀번호는 저장하지 않음, 공유 DB와 무관).
    private static readonly string LocalSettingsPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevManager", "local.json");

    private void LoadSavedLoginId()
    {
        try
        {
            if (!File.Exists(LocalSettingsPath)) return;
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(LocalSettingsPath));
            if (doc.RootElement.TryGetProperty("savedLoginId", out var v) && v.GetString() is { Length: > 0 } id)
            {
                LoginId = id;
                SaveLoginId = true;
            }
        }
        catch { /* 로컬 설정이 깨져도 로그인은 가능해야 한다 */ }
    }

    private void StoreLoginId(string loginId)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LocalSettingsPath)!);
            var json = System.Text.Json.JsonSerializer.Serialize(new { savedLoginId = SaveLoginId ? loginId : "" });
            File.WriteAllText(LocalSettingsPath, json);
        }
        catch { }
    }

    private DispatcherTimer? _timer;

    private DispatcherTimer StartTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        t.Tick += (_, _) => { if (IsMain) Reload(); };
        t.Start();
        return t;
    }

    [RelayCommand]
    private void Logout()
    {
        _me = null;
        IsMain = false;
        IsLogin = true;
        ShowDetail = ShowForm = ShowUserForm = false;
        Items.Clear();
    }

    partial void OnShowAllChanged(bool value) => Reload();

    private void Reload()
    {
        if (_me == null) return;
        var keep = SelectedItem?.Id;
        _suppress = true;
        Items.Clear();
        foreach (var r in _svc.List(_me.Id, ShowAll)) Items.Add(r);
        SelectedItem = keep == null ? null : Items.FirstOrDefault(x => x.Id == keep);
        _suppress = false;
        UnreadCount = _svc.UnreadCount(_me.Id);
        HasUnread = UnreadCount > 0;
        ListHeader = ShowAll ? $"전체 개발건 {Items.Count}건" : $"내게 배정된 개발건 {Items.Count}건";
    }

    [RelayCommand] private void Refresh() => Reload();

    public void CloseAllPanels()
    {
        ShowDetail = ShowForm = ShowUserForm = ShowComplete = false;
        SelectedItem = null;
    }

    partial void OnSelectedItemChanged(RequirementRow? value)
    {
        if (_suppress || _me == null || value == null) return;
        ShowForm = ShowUserForm = false;
        LoadDetail(value.Id);
        _svc.MarkRead(_me.Id, value.Id);
        Reload();
    }

    private static string D(DateOnly? d) => d?.ToString("yyyy-MM-dd") ?? "-";

    private void LoadDetail(int id)
    {
        var r = _svc.Detail(_me!.Id, id);
        if (r == null) { ShowDetail = false; return; }
        _detailId = r.Id;
        _detailVersion = r.Version;
        DetailDocNo = r.DocNo;
        DetailTitle = r.Title;
        DetailDesc = r.Description;
        DetailCategory = r.Category?.Name ?? "";
        DetailRequester = r.RequesterUser?.Name ?? "-";
        DetailDue = D(r.DueDate);
        DetailStart = D(r.DevStartDate);
        DetailEnd = D(r.DevEndDate);
        DetailDeploy = D(r.DeployDate);
        DetailAssignee = r.Assignee?.Name ?? "미배정";
        DetailMessage = "";
        DetailStatus = r.Status switch
        {
            RequirementStatus.Registered => "등록",
            RequirementStatus.InProgress => "진행",
            RequirementStatus.Review => "검토",
            RequirementStatus.Done => "개발완료",
            _ => "종료"
        };
        DetailCompletedModules = r.CompletedModules ?? "";
        DetailCompletedAt = r.CompletedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "";
        ShowCompletedInfo = !string.IsNullOrEmpty(r.CompletedModules);
        CanComplete = r.AssigneeId == _me.Id && r.Status != RequirementStatus.Done;
        CanEdit = IsAdmin || r.AssigneeId == _me.Id || r.CreatedById == _me.Id;
        if (IsAdmin)
        {
            AssignOptions.Clear();
            AssignOptions.Add(new Option(0, "미배정"));
            foreach (var o in _svc.ActiveUsers()) AssignOptions.Add(o);
            SelectedAssignee = AssignOptions.FirstOrDefault(o => o.Id == (r.AssigneeId ?? 0));
        }
        EditSource = r;
        ShowDetail = true;
    }

    private Requirement? EditSource { get; set; }

    private void LoadUserOptions()
    {
        UserOptions.Clear();
        foreach (var o in _svc.ActiveUsers()) UserOptions.Add(o);
    }

    [RelayCommand]
    private void Assign()
    {
        if (_me == null || SelectedAssignee == null) return;
        var err = _svc.Assign(_me.Id, _detailId, SelectedAssignee.Id == 0 ? null : SelectedAssignee.Id);
        DetailMessage = err ?? "배정했습니다";
        if (err == null) { LoadDetail(_detailId); DetailMessage = "배정했습니다"; Reload(); }
    }

    [RelayCommand]
    private void OpenComplete()
    {
        CompleteModules = CompleteError = "";
        ShowDetail = false;
        ShowComplete = true;
    }

    [RelayCommand]
    private void SaveComplete()
    {
        if (_me == null) return;
        var err = _svc.Complete(_me.Id, _detailId, _detailVersion, CompleteModules);
        if (err != null) { CompleteError = err; return; }
        ShowComplete = false;
        LoadDetail(_detailId);
        Reload();
    }

    [RelayCommand]
    private void CancelComplete()
    {
        ShowComplete = false;
        ShowDetail = true;
    }

    [RelayCommand]
    private void NewRequirement()
    {
        _editId = null;
        FormHeader = "개발 등록 (문서번호는 저장 시 자동 부여)";
        FormTitle = FormDesc = FormError = "";
        FormDue = FormStart = FormEnd = FormDeploy = null;
        LoadUserOptions();
        FormRequester = UserOptions.FirstOrDefault(o => o.Id == _me!.Id);
        FormCategory = Categories.FirstOrDefault();
        CanEditDevDates = false;
        CanEditCore = true;
        ShowDetail = ShowUserForm = false;
        ShowForm = true;
    }

    [RelayCommand]
    private void EditRequirement()
    {
        var r = EditSource;
        if (r == null) return;
        _editId = r.Id;
        _editVersion = _detailVersion;
        FormHeader = $"{r.DocNo} 수정";
        FormTitle = r.Title;
        FormDesc = r.Description;
        LoadUserOptions();
        FormRequester = UserOptions.FirstOrDefault(o => o.Id == r.RequesterUserId);
        FormDue = Dt(r.DueDate);
        FormStart = Dt(r.DevStartDate);
        FormEnd = Dt(r.DevEndDate);
        FormDeploy = Dt(r.DeployDate);
        FormCategory = Categories.FirstOrDefault(c => c.Id == r.CategoryId);
        CanEditDevDates = _me != null && r.AssigneeId == _me.Id;
        CanEditCore = IsAdmin;
        FormError = "";
        ShowDetail = false;
        ShowForm = true;
    }

    private static DateTime? Dt(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);

    private static DateOnly? Do(DateTime? d) => d is { } v ? DateOnly.FromDateTime(v) : null;

    [RelayCommand]
    private void SaveForm()
    {
        if (_me == null) return;
        var input = new RequirementInput(FormTitle, FormDesc, FormCategory?.Id ?? 0, FormRequester?.Id ?? 0, Do(FormDue), Do(FormStart), Do(FormEnd), Do(FormDeploy));
        int? openId;
        if (_editId is { } id)
        {
            var err = _svc.Update(_me.Id, id, _editVersion, input);
            if (err != null) { FormError = err; return; }
            openId = id;
        }
        else
        {
            var (r, err) = _svc.Register(_me.Id, input);
            if (r == null) { FormError = err ?? ""; return; }
            openId = r.Id;
        }
        ShowForm = false;
        ShowAll = IsAdmin || ShowAll;
        Reload();
        if (_editId == null)
        {
            // 신규 등록은 저장 후 창을 닫는다.
            CloseAllPanels();
            return;
        }
        if (openId is { } oid)
        {
            SelectedItem = Items.FirstOrDefault(x => x.Id == oid);
            if (SelectedItem == null) LoadDetail(oid);
        }
    }

    [RelayCommand] private void CancelForm() { ShowForm = false; if (_detailId != 0 && SelectedItem != null) ShowDetail = true; }

    [RelayCommand]
    private void OpenUserForm()
    {
        NewUserLoginId = NewUserName = NewUserPassword = UserFormMessage = "";
        NewUserJobTitle = JobTitles.FirstOrDefault();
        ShowDetail = ShowForm = false;
        ShowUserForm = true;
    }

    [RelayCommand]
    private void SaveUser()
    {
        if (_me == null) return;
        var err = _svc.CreateUser(_me.Id, NewUserLoginId, NewUserName, NewUserJobTitle?.Id, NewUserPassword);
        UserFormMessage = err ?? "계정을 만들었습니다";
        if (err == null) NewUserLoginId = NewUserName = NewUserPassword = "";
    }

    [RelayCommand] private void CloseUserForm() => ShowUserForm = false;
}
