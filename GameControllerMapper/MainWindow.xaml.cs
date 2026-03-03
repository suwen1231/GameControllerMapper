using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace GameControllerMapper
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //if (DataContext is MainViewModel vm && vm.IsRecordingMode)
            //{
            //    vm.RecordKey(e);
            //    e.Handled = true;
            //}
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
            base.OnClosed(e);
        }

        private void ConfigList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConfigList.SelectedItem != null)
            {
                ConfigList.ScrollIntoView(ConfigList.SelectedItem);
            }
        }
    }
}