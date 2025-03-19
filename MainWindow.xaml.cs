using System;
using System.Windows;
using RTDTrading;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Controls;

namespace CompositeMan;

public partial class MainWindow : Window
{
    private IRtdServer? _rtdServer;

    private int _serverState = -1;

    private readonly RtdUpdateEvent _updateEvent = new();

    private bool IsRtdConnected => _serverState == 1;

    private CancellationTokenSource? _cancellationTokenSource;

    private Task? _updateMarketDataTask;

    public MainWindow()
    {
        InitializeComponent();
        UpdateUi(false);
    }

    private void ButtonConnect_Click(object sender, RoutedEventArgs e)
    {
        // Check if the server is already connected
        if (_serverState == 1)
        {
            ButtonUpdate.Content = "ATUALIZAR";
            _cancellationTokenSource?.Cancel();
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

        MarketData? marketData = RequestDataFromRtd();
        UpdateUi(IsRtdConnected, marketData);
    }

    private void ButtonUpdate_Click(object sender, RoutedEventArgs e)
    {
        // If not connected, return
        if (_serverState != 1) return;

        if (ButtonUpdate.Content.ToString() == "ATUALIZAR")
        {
            ButtonUpdate.Content = "ATUALIZANDO";
            _cancellationTokenSource = new CancellationTokenSource();
            _updateMarketDataTask = Task.Run(() => UpdateMarketDataTask(_cancellationTokenSource.Token));
        }
        else
        {
            ButtonUpdate.Content = "ATUALIZAR";
            _cancellationTokenSource?.Cancel();
        }
    }

    private async Task InitializeRtdServerAsync()
    {
        await Dispatcher.InvokeAsync(() => { LabelStatusConnection.Text = "Iniciando servidor"; });

        try
        {
            // Create the RTD server
            _rtdServer = new RtdServer();

            // Start RTD server
            _serverState = _rtdServer.ServerStart(_updateEvent);

            await Dispatcher.InvokeAsync(() => { UpdateUi(IsRtdConnected); });
        }
        catch (Exception e)
        {
            await Dispatcher.InvokeAsync(() => { LabelStatusConnection.Text = $"Houve um erro. {e.Message}"; });
        }
    }

    private MarketData? RequestDataFromRtd()
    {
        if (_rtdServer == null) return null;

        object? results;

        const string ticker = "WINFUT_F_0";

        // Abertura do Dia Atual
        Array topicAbe = new object[] { ticker, "ABE" };
        results = _rtdServer.ConnectData(901, topicAbe, false);
        string openPrice = results.ToString() ?? "Indisponível";

        // Fechamento do Dia Anterior
        Array topicFec = new object[] { ticker, "FEC" };
        results = _rtdServer.ConnectData(902, topicFec, false);
        string closePrice = results.ToString() ?? "Indisponível";

        // Último Preço do Dia Atual
        Array topicUlt = new object[] { ticker, "ULT" };
        results = _rtdServer.ConnectData(903, topicUlt, true);
        string lastPrice = results.ToString() ?? "Indisponível";

        // Média Móvel
        Array topicMa = new object[] { ticker, "3" };
        results = _rtdServer.ConnectData(904, topicMa, true);
        string movingAverage = results.ToString() ?? "Indisponível";

        MarketData marketData = new()
        {
            Ticker = ticker,
            OpenPrice = openPrice,
            ClosePrice = closePrice,
            LastPrice = lastPrice,
            MovingAverage = movingAverage
        };

        return marketData;
    }

    private void UpdateMarketDataTask(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            // Call your method here
            MarketData? marketData = UpdateRtdData();

            if (marketData != null)
                Dispatcher.InvokeAsync(() => { UpdateUi(IsRtdConnected, marketData); });

            // Delay for 0.1 second
            Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).Wait(cancellationToken);
        }
    }

    private MarketData? UpdateRtdData()
    {
        if (_rtdServer == null) return null;

        int topicsCount = 0;
        object[,]? results = (object[,])_rtdServer.RefreshData(ref topicsCount);

        if (topicsCount == 0) return null;

        if (results.GetLength(1) <= 0) return null;

        string last = string.Empty, ma = string.Empty;

        for (int columnIndex = 0; columnIndex < results.GetLength(1); columnIndex++)
        {
            object topicId = results[0, columnIndex];
            object topicValue = results[1, columnIndex];

            switch ((int)topicId)
            {
                case 903:
                    last = topicValue.ToString() ?? string.Empty;
                    break;
                case 904:
                    ma = topicValue.ToString() ?? string.Empty;
                    break;
            }
        }

        MarketData marketData = new()
        {
            LastPrice = last,
            MovingAverage = ma
        };

        return marketData;

    }

    private void UpdateUi(bool isConnected, MarketData? marketData = null)
    {
        ButtonConnect.Content = isConnected ? "DESCONECTAR" : "CONECTAR";
        LabelStatusConnection.Text = isConnected ? "Conectado" : "Desconectado";

        ButtonRequest.IsEnabled = isConnected;
        ButtonUpdate.IsEnabled = isConnected;

        if (marketData != null)
        {
            UpdateLabel(LabelValueTicker, marketData.Ticker);
            UpdateLabel(LavelValueOpen, marketData.OpenPrice);
            UpdateLabel(LabelValueClose, marketData.ClosePrice);
            UpdateLabel(LabelValueLast, marketData.LastPrice);
            UpdateLabel(LabelValueMovingAverage, marketData.MovingAverage);
        }
        else
        {
            LabelValueTicker.Content = "";
            LavelValueOpen.Content = "";
            LabelValueClose.Content = "";
            LabelValueLast.Content = "";
            LabelValueMovingAverage.Content = "";
        }
    }

    private void UpdateLabel(Label label, string? value)
    {
        if (value == null) return;
        string? currentValue = label.Content.ToString();
        if (currentValue == value || value == string.Empty) return;
        label.Content = value;
    }

    private void DisconnectRtdData()
    {
        if (_rtdServer == null) return;
        _rtdServer.DisconnectData(901);
        _rtdServer.DisconnectData(902);
        _rtdServer.DisconnectData(903);
        _rtdServer.DisconnectData(904);
    }

    private void TerminateRtdServer()
    {
        // Shutdown the RTD server.
        if (!IsRtdConnected) return;
        if (_rtdServer == null) return;

        _rtdServer.ServerTerminate();
        _serverState = 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        _updateMarketDataTask?.Wait();
        base.OnClosed(e);
    }
}

public class RtdUpdateEvent : IRTDUpdateEvent
{
    public long Count { get; set; }
    public int HeartbeatInterval { get; set; }

    public RtdUpdateEvent()
    {
        // Do not call the RTD Heartbeat() method.
        HeartbeatInterval = 100;
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

    public string? OpenPrice { get; set; }

    public string? ClosePrice { get; set; }

    public string? LastPrice { get; set; }

    public string? MovingAverage { get; set; }
}