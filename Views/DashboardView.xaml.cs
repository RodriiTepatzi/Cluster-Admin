using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using P2P_UAQ_Server.Views;
using P2P_UAQ_Server.ViewModels;
using P2P_UAQ_Server.Models;

namespace P2P_UAQ_Server.Views
{
    public partial class DashboardView : Window
    {
        private DashboardViewModel viewModel;

        public DashboardView(DashboardViewModel viewModel)
        {
            InitializeComponent();
            this.DataContext = viewModel;
            this.viewModel = viewModel;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
                viewModel.TurnOffServer();
                Application.Current.Shutdown();
            
        }

        private void BtnStopServer_Click(object sender, RoutedEventArgs e)
        {
            viewModel.TurnOffServer();
        }
    }
}
