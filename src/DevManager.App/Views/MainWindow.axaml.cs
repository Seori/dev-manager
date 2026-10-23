using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using DevManager.App.ViewModels;

namespace DevManager.App.Views;

public partial class MainWindow : Window
{
    private DetailWindow? _panel;

    public MainWindow()
    {
        InitializeComponent();
        DragArea.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        CloseButton.Click += (_, _) => Close();
        Opened += (_, _) =>
        {
            if (DataContext is MainWindowViewModel v && v.IsLogin) FocusLogin(v);
        };
        DataContextChanged += (_, _) =>
        {
            if (DataContext is not MainWindowViewModel vm) return;
            vm.PropertyChanged += OnVmChanged;
            ApplySize(vm);
        };
    }

    private void OnVmChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm) return;
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.IsMain):
            case nameof(MainWindowViewModel.IsSetup):
                ApplySize(vm);
                break;
            case nameof(MainWindowViewModel.IsLogin):
                ApplySize(vm);
                if (vm.IsLogin) FocusLogin(vm);
                break;
            case nameof(MainWindowViewModel.ShowDetail):
            case nameof(MainWindowViewModel.ShowForm):
            case nameof(MainWindowViewModel.ShowUserForm):
            case nameof(MainWindowViewModel.ShowComplete):
                SyncPanel(vm);
                break;
        }
    }

    // 저장된 아이디가 있으면 비밀번호로, 없으면 아이디로 포커스를 준다.
    private void FocusLogin(MainWindowViewModel vm) =>
        Dispatcher.UIThread.Post(() =>
        {
            var box = string.IsNullOrEmpty(vm.LoginId) ? LoginIdBox : PasswordBox;
            box.Focus();
        }, DispatcherPriority.Input);

    // 로그인·초기 설정 화면은 입력 영역에 딱 맞게, 메인은 메신저 크기로 둔다.
    private void ApplySize(MainWindowViewModel vm)
    {
        if (vm.IsMain)
        {
            SizeToContent = SizeToContent.Manual;
            Width = 400;
            Height = 680;
        }
        else
        {
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }

    // 상세·등록·계정 추가는 메인 옆의 별도 창으로 열고, 모두 닫히면 창도 닫는다.
    private void SyncPanel(MainWindowViewModel vm)
    {
        var any = vm.ShowDetail || vm.ShowForm || vm.ShowUserForm || vm.ShowComplete;
        if (any)
        {
            if (_panel == null)
            {
                _panel = new DetailWindow { DataContext = vm, WindowStartupLocation = WindowStartupLocation.Manual };
                _panel.Position = new PixelPoint(Position.X + (int)(Bounds.Width * DesktopScaling) + 8, Position.Y);
                _panel.Closed += (_, _) =>
                {
                    _panel = null;
                    vm.CloseAllPanels();
                };
                _panel.Show(this);
            }
            else
            {
                _panel.Activate();
            }
        }
        else if (_panel != null)
        {
            var p = _panel;
            _panel = null;
            p.Close();
        }
    }
}
