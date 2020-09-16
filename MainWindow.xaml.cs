using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ClientWPF
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int port = 8080;
        private TcpClient tcpClient;
        private string _message;
        private string id;
        private static IPAddress ip = IPAddress.Parse("192.168.0.77");
        private System.Timers.Timer t;
        private bool isEnd;
        private bool IsMaximumWindow;

        /// <summary>
        /// ИгровоеВремя
        /// </summary>
        private int PlayTime { get; set; }

        /// <summary>
        /// Сообщение сервера
        /// </summary>
        private string Message
        {
            get
            {
                return _message;
            }
            set
            {
                _message = value;
                SetOption();
            }
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        static extern bool EnableMenuItem(IntPtr hMenu, uint uIDEnableItem, uint uEnable);
         
         
        const uint MF_BYCOMMAND = 0x00000000;
        const uint MF_GRAYED = 0x00000001;
         
        const uint SC_CLOSE = 0xF060;
         
        /// <summary>
        /// Блочит TSKMNG
        /// </summary>
        private void SetCtrlAltOption()
        {
            if (File.Exists(@"Software\Microsoft\Windows\CurrentVersion\Policies\System\DisableTaskMgr")) return;
            RegistryKey regkey;
            string keyValueInt = "1";
            string subKey = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
            try
            {
                regkey = Registry.CurrentUser.CreateSubKey(subKey);
                regkey.SetValue("DisableTaskMgr", keyValueInt);
                regkey.SetValue("DisableRegistryTools", keyValueInt);
                regkey.Close();
            }
            catch (Exception ex)
            {
            }
        }

        /// <summary>
        /// Уменьшить окно
        /// </summary>
        private void SetMinimazeApp()
        {
            if (Image.Visibility == Visibility.Visible)
            {
                SetMinimazeAppOptions();
            }
            IsMaximumWindow = false;
            Topmost = false;
            if (t != null) t.Stop();
            progressbar.Maximum = PlayTime;
            progressbar.Minimum = 0;
            progressbar.Value = 0;
            label.Content = $"Осталось: {PlayTime} минут";
            t = new System.Timers.Timer();
            t.AutoReset = false;
            t.Elapsed += new ElapsedEventHandler(t_Elapsed);
            t.Interval = 60_000;
            t.Start();
        }
        private void SetVipMinimazeApp()
        {
            PlayTime = 0;
            IsMaximumWindow = false;
            label.Content = $"Прошло: {PlayTime} минут";
            if (t != null) t.Stop();
            Topmost = false;
            t = new System.Timers.Timer();
            t.AutoReset = false;
            t.Elapsed += new ElapsedEventHandler(t_VipElapsed);
            t.Interval = 60_000;
            t.Start();
            progressbar.Value = progressbar.Maximum;
            if (Image.Visibility == Visibility.Visible)
            {
                SetMinimazeAppOptions();
            }
        }
        private void t_Elapsed(object sender, ElapsedEventArgs e)
        {
            App.Current.Dispatcher.Invoke((System.Action)delegate
            {
                PlayTime--;
                progressbar.Value++;
                label.Content = $"Осталось: {PlayTime} минут";
                if (PlayTime == 0)
                {
                    t.Close();
                    t.Stop();
                    label.Content = $"Осталось: 1 минута";
                    return;
                }
                t.Stop();
                t.Close();
                t.Start();
            });
            
        }
         
        private void t_VipElapsed(object sender, ElapsedEventArgs e)
        {
            App.Current.Dispatcher.Invoke((System.Action)delegate
            {
                PlayTime++;
                progressbar.Value++;
                label.Content = $"Прошло: {PlayTime} минут";
                t.Stop();
                t.Close();
                t.Start();
            });

        }

        /// <summary>
        /// Настройка маленького окна
        /// </summary>
        private void SetMinimazeAppOptions()
        {
            Top = 0;
            Left = 0;
            WindowOption.WindowState = WindowState.Minimized;
            WindowOption.MinHeight = 200;
            WindowOption.MinWidth = 350;
            WindowOption.Height = 200;
            WindowOption.Width = 350;
            WindowOption.MaxHeight = 210;
            WindowOption.MaxWidth = 360;
            WindowOption.VerticalAlignment = VerticalAlignment.Center;
            WindowOption.HorizontalAlignment = HorizontalAlignment.Center;
            Image.Visibility = Visibility.Hidden;
        }

        private async void SetMaximizeApp()
        {
            IsMaximumWindow = true;
            while (IsMaximumWindow)
            {
                this.Show();
                this.Activate();
                this.Visibility = Visibility.Visible;
                Topmost = true;
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;
                Image.Visibility = Visibility.Visible;
                Top = -35;
                Left = -10;
                Height = screenHeight + 50;
                Width = screenWidth + 40;
                MinHeight = screenHeight + 50;
                MinWidth = screenWidth + 40;
                this.WindowState = WindowState.Normal;
                await Task.Delay(300);
            }
        }

        /// <summary>
        /// Понимает что передал Сервер
        /// </summary>
        private void SetOption()
        {
            if (int.TryParse(Message, out _))
            {
                PlayTime = int.Parse(Message);
                SetMinimazeApp();
            }
            else if(bool.TryParse(Message, out _))
            {
                if (bool.Parse(Message) == true)
                {
                    SetMaximizeApp();
                }
                else
                {
                    SetVipMinimazeApp();
                }
            }
            else
            {
                SetMaximizeApp();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            SetStartupSettings();
            SetMaximizeApp();
            SetCtrlAltOption();
            Task thread = new Task(Connect);
            thread.Start();
            Task thread1 = new Task(waitForServerCommand);
            thread1.Start();
        }
        private void SetStartupSettings()
        {
            try
            {
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
                Assembly curAssembly = Assembly.GetExecutingAssembly();
                key.SetValue(curAssembly.GetName().Name, curAssembly.Location);
            }
            catch { }
        }

        /// <summary>
        /// Коннект к серверу
        /// </summary>
        private void Connect()
        {
            var message = Environment.UserName;
            while (true)
            {
                try
                {
                    tcpClient = new TcpClient();
                    tcpClient.Connect(ip, port);
                    if (isEnd) return;
                    tcpClient.Client.Send(Encoding.UTF8.GetBytes(message));
                    tcpClient.Client.ReceiveTimeout = 500;
                    tcpClient.Client.SendTimeout = 500;
                    byte[] buffer = new byte[256];
                    var answer = new StringBuilder();
                    if (IsTcpClientConnected())
                    {
                        while (true)
                        {
                            if (!IsTcpClientConnected()) break;
                            Thread.Sleep(5000);
                        }
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(8000);
                }
            }
        }

        private void Disconnect(string id)
        {
            byte[] data = Encoding.UTF8.GetBytes(id);
            tcpClient.Client.Send(data);
            // Закрываем потоки
            tcpClient.Close();
        }

        private bool IsTcpClientConnected()
        {
            IPGlobalProperties ipProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnections = ipProperties.GetActiveTcpConnections().Where(x => x.LocalEndPoint.Equals(tcpClient.Client.LocalEndPoint) && x.RemoteEndPoint.Equals(tcpClient.Client.RemoteEndPoint)).ToArray();

            if (tcpConnections != null && tcpConnections.Length > 0)
            {
                TcpState stateOfConnection = tcpConnections.First().State;
                if (stateOfConnection == TcpState.Established)
                {
                    return true;
                }
                else
                {
                    return false;
                }

            }
            return false;
        }

        /// <summary>
        /// Пытается отправить сервера
        /// </summary>
        private void waitForServerCommand()
        {
            while (true)
            {
                try
                {
                    byte[] data = new byte[256];
                    StringBuilder response = new StringBuilder();
                    if (tcpClient != null)
                    {
                        do
                        {
                            int bytes = tcpClient.Client.Receive(data);
                            response.Append(Encoding.UTF8.GetString(data, 0, bytes));
                        }
                        while (tcpClient.Client.Available > 0); // пока данные есть в потоке
                        App.Current.Dispatcher.Invoke((System.Action)delegate
                        {
                            Message = response.ToString();
                        });
                        tcpClient.Client.Send(Encoding.UTF8.GetBytes(Message));
                        continue;
                    }
                }
                catch (Exception)
                {
                    Thread.Sleep(500);
                    continue;
                }
            }
        }


        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Disable close button
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            IntPtr hMenu = GetSystemMenu(hwnd, false);
            if (hMenu != IntPtr.Zero)
            {
                EnableMenuItem(hMenu, SC_CLOSE, MF_BYCOMMAND | MF_GRAYED);
            }
        }

        private void WindowOption_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (IsMaximumWindow) WindowState = WindowState.Normal;
        }

        /// <summary>
        /// When comp is shutdowning
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainWindow_SessionEnding(object sender, SessionEndingCancelEventArgs e)
        {
            Disconnect(id);
            SetStartupSettings();
            isEnd = true;
        }

        private void WindowOption_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = true;
        }

        private void WindowOption_StateChanged(object sender, EventArgs e)
        {
            if (IsMaximumWindow)
            {
                this.WindowState = WindowState.Normal;
            }
        }

        private void WindowOption_KeyDown(object sender, KeyEventArgs e)
        {

        }
    }
}
