using System.Windows;
using System.Windows.Controls;
using CAApplication.ViewModels;

namespace CAApplication;

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
                    if (MasterPasswordBox != null && MasterPasswordBox.Password != vm.LoginPassword)
                    {
                        MasterPasswordBox.Password = vm.LoginPassword;
                    }
                }
            };
        }
    }

    private void MasterPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox pb)
        {
            if (vm.IsPasswordMasked && vm.LoginPassword != pb.Password)
            {
                vm.LoginPassword = pb.Password;
            }
        }
    }

    private void LoginInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
        {
            if (DataContext is MainViewModel vm && !vm.IsAuthenticated)
            {
                if (sender is PasswordBox pb)
                {
                    vm.LoginPassword = pb.Password;
                }
                if (vm.PerformLoginCommand.CanExecute(null))
                {
                    vm.PerformLoginCommand.Execute(null);
                }
            }
        }
    }
}