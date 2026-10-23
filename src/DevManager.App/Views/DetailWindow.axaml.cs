using Avalonia.Controls;
using Avalonia.Input;

namespace DevManager.App.Views;

public partial class DetailWindow : Window
{
    public DetailWindow()
    {
        InitializeComponent();
        DragArea.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        CloseButton.Click += (_, _) => Close();
    }
}
