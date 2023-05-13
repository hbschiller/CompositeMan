using System.ComponentModel;
using System.Threading;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using RTDTrading;
using Constants = CompositeMan.Utils.Constants;
using System.Threading.Tasks;

namespace CompositeMan
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string RtdProgId = "rtdtrading.rtdserver";

        private readonly RtdUpdateEvent _updateEvent = new();

        private IRtdServer? _rtdServer;

        private int _serverState = -1;

        // Topic Ids
        int[] topicIDs = { 101, 102, 103 };

        private bool IsRtdConnected => _serverState == 1;

        public MainWindow()
        {
            InitializeComponent();
            Task.Run(InitializeRtdServerAsync);
        }

        private async Task InitializeRtdServerAsync()
        {
            try
            {
                await Dispatcher.InvokeAsync(() => { LabelStatusConnection.Text = "Iniciando servidor"; });

                // Create the RTD server
                var rtdType = Type.GetTypeFromProgID(RtdProgId);
                if (rtdType == null)
                {
                    throw new Exception("Houve uma olhada ao carregar o tipo do objeto");
                }

                _rtdServer = Activator.CreateInstance(rtdType) as IRtdServer;

                // Start the RTD server
                if (_rtdServer == null)
                {
                    throw new Exception("Houve uma olhada ao carregar o servidor");
                }

                _serverState = _rtdServer.ServerStart(_updateEvent);
                
                // Server Started. Update the Label on the UI thread
                await Dispatcher.InvokeAsync(() => { UpdateUi(IsRtdConnected); });
            }
            catch (Exception e)
            {
                await Dispatcher.InvokeAsync(() => { LabelStatusConnection.Text = $"Houve um erro. {e.Message}"; });
            }
        }

        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            // Check if the server is already connected
            if (_serverState == 1)
            {
                DisconnectRtd();
                TerminateRtd();
                UpdateUi(IsRtdConnected);
                return;
            }

            // Not Connected, try to connect
            Task.Run(InitializeRtdServerAsync);
        }

        private void ButtonRequest_Click(object sender, RoutedEventArgs e)
        {
            // If not connected, return
            if (_serverState != 1) return;

            MarketData? marketData = GetDataRtd(Constants.TickerIndfut);
            UpdateUi(IsRtdConnected, marketData);
        }

        private MarketData? GetDataRtd(string ticker)
        {
            if (_rtdServer == null) return null;

            // Topic Names
            Object[] topics = new Object[2];
            topics[0] = ticker;

            // Connect to the RTD server and retrieve data
            object? result;

            topics[1] = "ABE";
            result = _rtdServer.ConnectData(topicIDs[0], topics, true);
            double.TryParse(result.ToString(), out var openPrice);

            topics[1] = "FEC";
            result = _rtdServer.ConnectData(topicIDs[1], topics, true);
            double.TryParse(result.ToString(), out var closePrice);

            topics[1] = "ULT";
            result = _rtdServer.ConnectData(topicIDs[2], topics, true);
            double.TryParse(result.ToString(), out var lastPrice);

            return new MarketData(openPrice, closePrice, lastPrice);
        }

        private void UpdateUi(bool isConnected, MarketData? marketData = null)
        {
            ButtonConnect.Content = isConnected ? "DESCONECTAR" : "CONECTAR";
            LabelStatusConnection.Text = isConnected ? "Conectado" : "Desconectado";
            
            if (marketData != null)
            {
                LavelValueOpen.Content = marketData.OpenPrice.ToString(CultureInfo.InvariantCulture);
                LabelValueClose.Content = marketData.ClosePrice.ToString(CultureInfo.InvariantCulture);
                LabelValueLast.Content = marketData.LastPrice.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                LavelValueOpen.Content = "";
                LabelValueClose.Content = "";
                LabelValueLast.Content = "";
            }
        }
        
        private void DisconnectRtd()
        {
            if (!IsRtdConnected) return;
            if (_rtdServer == null) return;

            foreach (var t in topicIDs)
            {
                _rtdServer.DisconnectData(t);
            }
        }

        private void TerminateRtd()
        {
            // Shutdown the RTD server.
            if (!IsRtdConnected) return;
            if (_rtdServer == null) return;

            _rtdServer.ServerTerminate();
            _serverState = 0;
        }


        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class RtdUpdateEvent : IRTDUpdateEvent
    {
        public long Count { get; set; }
        public int HeartbeatInterval { get; set; }

        public RtdUpdateEvent()
        {
            // Do not call the RTD Heartbeat() method.
            HeartbeatInterval = -1;
        }

        public void Disconnect()
        {
            // Do nothing.
        }

        public void UpdateNotify()
        {
            Count++;
        }
    }

    public class MarketData
    {
        public double OpenPrice { get; set; }

        public double ClosePrice { get; set; }

        public double LastPrice { get; set; }

        public MarketData(double openPrice, double closePrice, double lastPrice)
        {
            OpenPrice = openPrice;
            ClosePrice = closePrice;
            LastPrice = lastPrice;
        }
    }
}