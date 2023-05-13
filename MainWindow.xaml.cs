using System.ComponentModel;
using System.Threading;
using System;
using System.Windows;
using RTDTrading;
using Constants = CompositeMan.Utils.Constants;
using System.Threading.Tasks;

namespace CompositeMan
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private const string RtdProgId = "rtdtrading.rtdserver";

        private IRtdServer? _rtdServer;

        object? ret;
        string? connected;
        bool firstUpdate = true;
        int heartbeatStatus, btnStatus;
        double[] source_DOL = { 0, 0, 0 };

        BackgroundWorker? backgroundWorker;

        private double _trend;
        private double _axisMax;
        private double _axisMin;

        const string tpIDA = "101", tpIDF = "102", tpIDU = "103";

        public MainWindow()
        {
            InitializeComponent();
            InitializeTask();
        }

        void InitializeTask()
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            CancellationToken cancellationToken = cancellationTokenSource.Token;
            Task task = DoWorkAsync(cancellationToken);
        }

        private async Task DoWorkAsync(CancellationToken cancellationToken)
        {
            // Do your stuff here

            int topicCount = 0;
            Console.WriteLine("ScApp >> init topicCount = {0}", topicCount);
            double value;

            while (heartbeatStatus == 1)
            {
                // Check that the RTD server is still alive.

                if (!cancellationToken.IsCancellationRequested)
                {
                    heartbeatStatus = _rtdServer.Heartbeat();
                    Console.WriteLine("ScApp >> status for 'Heartbeat()' = {0}", heartbeatStatus.ToString());

                    // Get data from the RTD server.
                    object[,] r = new object[2, 3];
                    r = (object[,])_rtdServer.RefreshData(topicCount);

                    Console.WriteLine("ScApp >> RefreshData topicCount = {0}", topicCount);
                    //Console.WriteLine("ScApp >> retval for 'RefreshData()' = {0}", r[1, 0].ToString());

                    if (r.Length > 0)
                    {
                        for (int i = 0; i < r.Length / 2; i++)
                        {
                            value = 0;
                            double.TryParse(r[1, i].ToString(), out value);
                            Console.WriteLine("ScApp >> valor = {0} para topic = {1} e r = {2}", r[1, i].ToString(),
                                r[0, i].ToString(), r.Length);

                            if (r[0, i].ToString() == tpIDU)
                            {
                                await Dispatcher.InvokeAsync(() => { LabelUltimo.Content = value.ToString(); });
                            }
                        }

                        if (firstUpdate)
                        {
                            firstUpdate = false;
                            //resizeLadder();
                        }
                    }

                    Console.WriteLine("ScApp >> r.Length = {0}", r.Length);
                    await Task.Delay(100);
                }
                else
                {
                    break;
                }
            }
        }

        private void BackgroundWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = (BackgroundWorker)sender;

            //Do your stuff here

            int topicCount = 0;
            Console.WriteLine("ScApp >> init topicCount = {0}", topicCount);
            double value;

            while (heartbeatStatus == 1)
            {
                // Check that the RTD server is still alive.

                if (!worker.CancellationPending)
                {
                    heartbeatStatus = _rtdServer.Heartbeat();
                    Console.WriteLine("ScApp >> status for 'Heartbeat()' = {0}", heartbeatStatus.ToString());

                    // Get data from the RTD server.
                    object[,] r = new object[2, 3];
                    r = (object[,])_rtdServer.RefreshData(topicCount);

                    Console.WriteLine("ScApp >> RefreshData topicCount = {0}", topicCount);
                    //Console.WriteLine("ScApp >> retval for 'RefreshData()' = {0}", r[1, 0].ToString());

                    if (r.Length > 0)
                    {
                        for (int i = 0; i < r.Length / 2; i++)
                        {
                            value = 0;
                            double.TryParse(r[1, i].ToString(), out value);
                            Console.WriteLine("ScApp >> valor = {0} para topic = {1} e r = {2}", r[1, i].ToString(),
                                r[0, i].ToString(), r.Length);

                            if (r[0, i].ToString() == tpIDU)
                            {
                                this.Dispatcher.Invoke(() => { LabelUltimo.Content = value.ToString(); });
                            }
                        }

                        if (firstUpdate)
                        {
                            firstUpdate = false;
                            //resizeLadder();
                        }
                    }

                    Console.WriteLine("ScApp >> r.Length = {0}", r.Length);
                    Thread.Sleep(100);
                    worker.ReportProgress(0, "AN OBJECT TO PASS TO THE UI-THREAD");
                }
                else
                {
                    e.Cancel = true;
                    break;
                }
            }
        }


        private void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            if (btnStatus == 0)
            {
                ProcessRTD();
                if (IsRTDConnected())
                {
                    source_DOL = GetDataRTD(Constants.TickerIndfut);
                    if (source_DOL != null)
                    {
                        PrintResult(source_DOL);

                        // if (backgroundWorker.IsBusy != true)
                        // {
                        //     // Start the asynchronous operation.
                        //     backgroundWorker.RunWorkerAsync();
                        // }
                    }

                    ButtonConnect.Content = "DESCONECTAR";
                    LabelStatusConnection.Text = "Conectado";
                    btnStatus = 1;
                }
            }
            else if (btnStatus == 1)
            {
                DisconnectRTD(1);
                TerminateRTD();
                if (!IsRTDConnected())
                {
                    ButtonConnect.Content = "CONECTAR";
                    LabelStatusConnection.Text = "Desconectado";
                    btnStatus = 0;
                    LavelValueOpen.Content = "";
                    LabelFech.Content = "";
                    LabelUltimo.Content = "";

                    // if (backgroundWorker.WorkerSupportsCancellation == true)
                    // {
                    //     // Cancel the asynchronous operation.
                    //     backgroundWorker.CancelAsync();
                    // }
                }
            }
        }

        void ProcessRTD()
        {
            try
            {
                // Create the RTD server.
                Type rtd;
                Object rtdServer = null;
                rtd = Type.GetTypeFromProgID(RtdProgId);
                rtdServer = Activator.CreateInstance(rtd);
                _rtdServer = rtdServer as IRtdServer;
                Console.WriteLine("ScApp >> rtdServer = {0}", rtdServer);

                // Start the RTD server.
                IRTDUpdateEvent updateEvent = new IRTDUpdateEvent();
                ret = _rtdServer.ServerStart(updateEvent);
                connected = ret.ToString();
                Console.WriteLine("ScApp >> updateEvent = {0}", updateEvent.ToString());
                Console.WriteLine("ScApp >> ret for 'ServerStart()' = {0}", ret.ToString());
            }
            catch (Exception e)
            {
                LabelStatusConnection.Text = "Houve um erro, tente novamente.";
                Console.WriteLine("ScApp >> Error: {0} ", e.Message);
            }
        }

        private double[] GetDataRTD(string topic1)
        {
            double[] value = { 0, 0, 0 };
            int[] topicIDs = new int[3];
            topicIDs[0] = 101;
            topicIDs[1] = 102;
            topicIDs[2] = 103;
            // Connect Data.
            Object[] topics = new Object[2];
            topics[0] = topic1;

            topics[1] = "ABE";
            // TODO - Erro aqui - se o Profit não estiver aberto - precisa de uma forma de esperar
            ret = _rtdServer.ConnectData(topicIDs[0], topics, true);
            double.TryParse(ret.ToString(), out value[0]);
            Console.WriteLine("ScApp >> ret for 'ConnectData()' = {0} for topicID = {1}", ret.ToString(), topicIDs[0]);

            topics[1] = "FEC";
            ret = _rtdServer.ConnectData(topicIDs[1], topics, true);
            double.TryParse(ret.ToString(), out value[1]);
            Console.WriteLine("ScApp >> ret for 'ConnectData()' = {0} for topicID = {1}", ret.ToString(), topicIDs[1]);

            topics[1] = "ULT";
            ret = _rtdServer.ConnectData(topicIDs[2], topics, true);
            double.TryParse(ret.ToString(), out value[2]);
            Console.WriteLine("ScApp >> ret for 'ConnectData()' = {0} for topicID = {1}", ret.ToString(), topicIDs[2]);

            // Loop and wait for RTD to notify (via callback) that
            // data is available.
            heartbeatStatus = _rtdServer.Heartbeat();
            Console.WriteLine("ScApp >> status for 'Heartbeat()' = {0}", heartbeatStatus.ToString());

            return value;
        }

        private void RefreshDataRTD(object sender, DoWorkEventArgs e)
        {
            int topicCount = 0;
            Console.WriteLine("ScApp >> init topicCount = {0}", topicCount);
            double value;

            //while (status == 1 && !_shouldStop)
            while (heartbeatStatus == 1)
            {
                // Check that the RTD server is still alive.

                heartbeatStatus = _rtdServer.Heartbeat();
                if (heartbeatStatus == 0)
                {
                    LabelStatusConnection.Text = "Conectado";
                }
                else
                {
                    LabelStatusConnection.Text = "Houve um problema, tente conectar novamente.";
                }

                Console.WriteLine("ScApp >> status for 'Heartbeat()' = {0}", heartbeatStatus.ToString());

                // Get data from the RTD server.
                object[,] r = new object[2, 3];
                r = (object[,])_rtdServer.RefreshData(topicCount);

                Console.WriteLine("ScApp >> RefreshData topicCount = {0}", topicCount);
                //Console.WriteLine("ScApp >> retval for 'RefreshData()' = {0}", r[1, 0].ToString());

                if (r.Length > 0)
                {
                    for (int i = 0; i < r.Length / 2; i++)
                    {
                        value = 0;
                        double.TryParse(r[1, i].ToString(), out value);
                        Console.WriteLine("ScApp >> valor = {0} para topic = {1} e r = {2}", r[1, i].ToString(),
                            r[0, i].ToString(), r.Length);

                        //if (r[0, i].ToString() == )
                    }

                    if (firstUpdate)
                    {
                        firstUpdate = false;
                        //resizeLadder();
                    }
                }

                Console.WriteLine("ScApp >> r.Length = {0}", r.Length);
                Thread.Sleep(100);
            }
        }

        public void DisconnectRTD(int j)
        {
            // Disconnect from data topic.
            int[] topicIDs = new int[3];
            topicIDs[0] = 100 * j;
            topicIDs[1] = topicIDs[0]++;
            topicIDs[2] = topicIDs[1]++;
            for (int i = 0; i < topicIDs.Length; i++)
            {
                _rtdServer.DisconnectData(topicIDs[i]);
                Console.WriteLine("ScApp >> DisconnectData topicID = {0}", topicIDs[i]);
            }
        }

        public void TerminateRTD()
        {
            // Shutdown the RTD server.
            if (IsRTDConnected())
            {
                _rtdServer.ServerTerminate();
                Console.WriteLine("ScApp >> ServerTerminate");
            }
        }

        public void PrintResult(double[] source)
        {
            LavelValueOpen.Content = source[0].ToString();
            LabelFech.Content = source[1].ToString();
            LabelUltimo.Content = source[2].ToString();
        }

        private bool IsRTDConnected()
        {
            return ret.ToString() == "1";
        }


        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }

    public class IRTDUpdateEvent : RTDTrading.IRTDUpdateEvent
    {
        public long Count { get; set; }
        public int HeartbeatInterval { get; set; }

        public IRTDUpdateEvent()
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
        private double _abert, _fech, _ult;

        public double precoAbertura
        {
            get { return _abert; }
            set { _abert = value; }
        }

        public double precoFechamento
        {
            get { return _fech; }
            set { _fech = value; }
        }

        public double precoUltimo
        {
            get { return _ult; }
            set { _ult = value; }
        }
    }
}