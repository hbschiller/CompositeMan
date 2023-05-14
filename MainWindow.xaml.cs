using System.Threading;
using System;
using System.Globalization;
using System.Windows;
using RTDTrading;
using Constants = CompositeMan.Utils.Constants;
using System.Threading.Tasks;

namespace CompositeMan
{
    public partial class MainWindow : Window
    {
        private const string RtdProgId = "rtdtrading.rtdserver";

        private readonly RtdUpdateEvent _updateEvent = new();

        private IRtdServer? _rtdServer;

        private int _serverState = -1;

        private MarketData? indMarketData;

        private int heartbeatStatus;

        // Topics
        private readonly object[,] _topics =
        {
            { 101, 102, 103 },
            { "ABE", "FEC", "ULT" }
        };

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
                DisconnectRtdData();
                TerminateRtdServer();
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

            indMarketData = RequestDataFromRtd(Constants.TickerIndfut);
            UpdateUi(IsRtdConnected, indMarketData);
        }

        private MarketData? RequestDataFromRtd(string ticker)
        {
            if (_rtdServer == null) return null;

            // Topic Names
            Object[] topics = new Object[2];
            topics[0] = ticker;

            // Connect to the RTD server and retrieve data
            object? result;

            topics[1] = _topics[1, 0];
            result = _rtdServer.ConnectData((int)_topics[0, 0], topics, true);
            double.TryParse(result.ToString(), out var openPrice);

            topics[1] = _topics[1, 1];
            result = _rtdServer.ConnectData((int)_topics[0, 1], topics, true);
            double.TryParse(result.ToString(), out var closePrice);

            topics[1] = _topics[1, 2];
            result = _rtdServer.ConnectData((int)_topics[0, 2], topics, true);
            double.TryParse(result.ToString(), out var lastPrice);

            heartbeatStatus = _rtdServer.Heartbeat();

            MarketData marketData = new()
            {
                Ticker = ticker,
                OpenPrice = openPrice,
                ClosePrice = closePrice,
                LastPrice = lastPrice
            };

            return marketData;
        }

        private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(RefreshRtdDataAsync);
        }

        private async Task RefreshRtdDataAsync()
        {
            // Constants.TickerIndfut

            if (_rtdServer == null) return;

            while (_serverState == 1)
            {
                // Check that the RTD server is still alive.
                if (_rtdServer.Heartbeat() == 0)
                {
                    await Dispatcher.InvokeAsync(() => { LabelStatusConnection.Text = $"Houve um erro"; });
                    return;
                }

                int arraySize = _topics.GetLength(0);
                object[,] result = new object[2, arraySize];
                result = (object[,])_rtdServer.RefreshData(arraySize);

                if (result.GetLength(1) > 0)
                {
                    double open = 0, close = 0, last = 0;

                    for (int columnIndex = 0; columnIndex < result.GetLength(1); columnIndex++)
                    {
                        object topicId = result[0, columnIndex];
                        object topicValue = result[1, columnIndex];

                        open = (int)topicId == (int)_topics[0, 0]
                            ? double.Parse(topicValue.ToString() ?? string.Empty)
                            : 0;

                        close = (int)topicId == (int)_topics[0, 1]
                            ? double.Parse(topicValue.ToString() ?? string.Empty)
                            : 0;

                        last = (int)topicId == (int)_topics[0, 2]
                            ? double.Parse(topicValue.ToString() ?? string.Empty)
                            : 0;
                    }

                    MarketData marketData = new()
                    {
                        Ticker = Constants.TickerIndfut,
                        OpenPrice = open,
                        ClosePrice = close,
                        LastPrice = last
                    };

                    await Dispatcher.InvokeAsync(() => { UpdateUi(IsRtdConnected, marketData); });
                }

                Thread.Sleep(1000);
            }
        }

        private void UpdateUi(bool isConnected, MarketData? marketData = null)
        {
            ButtonConnect.Content = isConnected ? "DESCONECTAR" : "CONECTAR";
            LabelStatusConnection.Text = isConnected ? "Conectado" : "Desconectado";

            if (marketData != null)
            {
                LabelValueTicker.Content = marketData.Ticker;
                LavelValueOpen.Content = marketData.OpenPrice.ToString(CultureInfo.InvariantCulture);
                LabelValueClose.Content = marketData.ClosePrice.ToString(CultureInfo.InvariantCulture);
                LabelValueLast.Content = marketData.LastPrice.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                LabelValueTicker.Content = "";
                LavelValueOpen.Content = "";
                LabelValueClose.Content = "";
                LabelValueLast.Content = "";
            }
        }

        private void DisconnectRtdData()
        {
            if (!IsRtdConnected) return;
            if (_rtdServer == null) return;

            for (int columnIndex = 0; columnIndex < _topics.GetLength(1); columnIndex++)
            {
                _rtdServer.DisconnectData((int)_topics[0, columnIndex]);
            }
        }

        private void TerminateRtdServer()
        {
            // Shutdown the RTD server.
            if (!IsRtdConnected) return;
            if (_rtdServer == null) return;

            _rtdServer.ServerTerminate();
            _serverState = 0;
        }
        
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
        public string? Ticker { get; set; }

        public double OpenPrice { get; set; }

        public double ClosePrice { get; set; }

        public double LastPrice { get; set; }
    }
}