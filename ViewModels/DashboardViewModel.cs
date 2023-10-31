using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using P2P_UAQ_Server.Core;

namespace P2P_UAQ_Server.ViewModels
{
    public class DashboardViewModel:ViewModelBase
    {
        private bool _isViewVisible = true;
        private CoreHandler _handler;

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
   
        private List<string> serverStatusMessages = new List<string>();
        

        public DashboardViewModel()
        {
            _handler = CoreHandler.Instance;
            _handler.ServerStatusUpdated += OnServerStatusUpdated;
			_handler.PublicMessageReceived += _handler_PublicMessageReceived;
        }

		private void _handler_PublicMessageReceived(object? sender, Core.Events.MessageReceivedEventArgs e)
		{
            serverStatusMessages.Add(e.Message);
			OnPropertyChanged(nameof(AllServerStatusMessages));
		}

		public string AllServerStatusMessages
        {
            get { return string.Join(Environment.NewLine, serverStatusMessages); }
        }

        private void OnServerStatusUpdated(string status)
        {
            
            serverStatusMessages.Add(status);           
            OnPropertyChanged(nameof(AllServerStatusMessages));
        }

        public void TurnOffServer()
        {
            _handler.StopServer();
        }

    }
}
