using System.Windows;
using System.Windows.Controls;
using DesktopApp.ViewModels;

namespace DesktopApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsPasswordMasked) && vm.IsPasswordMasked)
                {
                    if (StorePasswordBox != null && StorePasswordBox.Password != vm.LoginPassword)
                    {
                        StorePasswordBox.Password = vm.LoginPassword;
                    }
                }
            };
        }
    }

    private void StorePasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox pb)
        {
            if (vm.IsPasswordMasked && vm.LoginPassword != pb.Password)
            {
                vm.LoginPassword = pb.Password;
            }
        }
    }
}