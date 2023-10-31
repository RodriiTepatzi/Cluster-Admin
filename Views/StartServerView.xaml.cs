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

namespace P2P_UAQ_Server.Views
{
  
    public partial class StartServerView : Window
    {
        public StartServerView()
        {
            InitializeComponent();

            txtDirIP.Text = "Dirección IPv4";
            txtDirIP.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E")); 
            txtPort.Text = "Puerto";
            txtPort.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E"));
            txtUsers.Text = "Número máximo de usuarios";
            txtUsers.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E"));


            txtDirIP.GotFocus += (sender, e) =>
            {
                if (txtDirIP.Text == "Dirección IPv4")
                {
                    txtDirIP.Text = "";
                    txtDirIP.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#303030"));
                }
            };

            txtDirIP.LostFocus += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtDirIP.Text))
                {
                    txtDirIP.Text = "Dirección IPv4";
                    txtDirIP.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E"));
                }
            };

            txtPort.GotFocus += (sender, e) =>
            {
                if (txtPort.Text == "Puerto")
                {
                    txtPort.Text = "";
                    txtPort.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#303030"));
                }
            };

            txtPort.LostFocus += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtPort.Text))
                {
                    txtPort.Text = "Puerto";
                    txtPort.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E"));
                }
            };

            txtUsers.GotFocus += (sender, e) =>
            {
                if (txtUsers.Text == "Número máximo de usuarios")
                {
                    txtUsers.Text = "";
                    txtUsers.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#303030"));
                }
            };

            txtUsers.LostFocus += (sender, e) =>
            {
                if (string.IsNullOrWhiteSpace(txtUsers.Text))
                {
                    txtUsers.Text = "Número máximo de usuarios";
                    txtUsers.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9E9E9E"));
                }
            };
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton==MouseButtonState.Pressed)
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
            Application.Current.Shutdown();
        }

    }
}
