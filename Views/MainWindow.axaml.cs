using System;
using Avalonia.Controls;
using Avalonia.Input;
using RoverExplorer1NodoMandoPC.ViewModels;

namespace RoverExplorer1NodoMandoPC.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            this.Opened += (s, e) =>
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    var handle = this.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    vm.SetWindowHandle(handle);
                }
            };
        }

        private void OnCommandKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && DataContext is MainWindowViewModel vm)
            {
                vm.SendCustomCommandCommand.Execute(null);
            }
        }
    }
}
