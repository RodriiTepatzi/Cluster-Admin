using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using P2P_UAQ_Server.Models;
using P2P_UAQ_Server.Views;
using P2P_UAQ_Server.ViewModels;
using P2P_UAQ_Server.Core;

namespace P2P_UAQ_Server.ViewModels
{
    public class StartServerViewModel:ViewModelBase
    {
        //Fields
        private string _dirIP;

        private string _port;
        private string _users;
        
        //private string _errorMessage;
        private bool _isViewVisible = true;
        private bool _isServerRunning = false;
        private object? _serverView;

        public string DirIP 
        {
            get
            {
                return _dirIP;
            }
            set
            {
                _dirIP = value;
                OnPropertyChanged(nameof(DirIP));
            }    
        }
        public string Port 
        {
            get 
            { 
                return _port; 
            }
            set 
            { 
                _port = value; 
                OnPropertyChanged(nameof(Port));
            } 
        }
        public string Users 
        {
            get
            {
                return _users;
            }
            set 
            { 
                _users = value; 
                OnPropertyChanged(nameof(Users));
            } 
        }

        public bool IsViewVisible 
        {
            get
            {
                return _isViewVisible;
            }
            set
            {
                _isViewVisible = value;
                OnPropertyChanged(nameof(IsViewVisible));
            } 
        }

        public bool IsServerRunning 
        {
            get
            { 
                return _isServerRunning; 
            }
            set
            {
                _isServerRunning = value;
            } 

        }

        public object ServerView
        {
            get
            {
                return _serverView!;
            }
            set
            { 
                _serverView = value; 
            }
        }
        //Commands
        public ICommand StartServerCommand { get; }
        


        //Construtor
        public StartServerViewModel()
        {

			_dirIP = "127.0.0.1";
		    _port = "8000";
		    _users = "20";
		    StartServerCommand = new ViewModelCommand(ExecuteStartServerCommand, CanExecuteStartServerCommand);
        }

        private bool CanExecuteStartServerCommand(object obj)
        {
            bool validData;
            if (string.IsNullOrWhiteSpace(DirIP) || string.IsNullOrWhiteSpace(Port) || string.IsNullOrWhiteSpace(Users))  
            { 
                validData = false;
            }
            else
            {
                validData = true;
            }
            return validData;
        }

        private void ExecuteStartServerCommand(object obj)
        {
			ShowDashboardView();
			CloseWindow();
			CoreHandler.Instance.InitializeLocalServer(DirIP, int.Parse(Port), Users);
			IsServerRunning = true;
            
        }

        private void CloseWindow()
        {
            Application.Current.Windows.OfType<StartServerView>().FirstOrDefault()?.Close();
        }

        private void ShowDashboardView()
        {
            var dashViewModel = new DashboardViewModel();
            var dashView = new DashboardView(dashViewModel);
            dashView.Show();
        }
    }
}
