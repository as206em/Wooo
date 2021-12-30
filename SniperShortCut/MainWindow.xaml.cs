using Microsoft.Win32;
using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using WoowShortCuts;

namespace WooowShortCuts
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ShortCutImages = new List<Rectangle>();
            imagename = new List<string>();
            EditingThisImage = new Rectangle();

            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.X, KeyModifiers.Alt);//show
            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.S, KeyModifiers.Alt);//screen shot
            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.O, KeyModifiers.Alt);//screen off
            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.B, KeyModifiers.Alt);//Change Brightness
            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.C, KeyModifiers.Alt);//Change Color
            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.M, KeyModifiers.Alt);//Change Color Mode
            HotKeyManager.RegisterHotKey(System.Windows.Forms.Keys.Q, KeyModifiers.Control);//Change Color Mode

            HotKeyManager.HotKeyPressed += new EventHandler<HotKeyEventArgs>(HotKeyManager_HotKeyPressed);
        }

        private void SystemEvents_SessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            if (e.Reason == SessionSwitchReason.SessionLock)
            {
                sendDataSerial("f", new byte[] { });
            }
            else if (e.Reason == SessionSwitchReason.SessionUnlock)
            {
                colorIndex = 1;
                sendDataSerial("n", new byte[] { 255, 255, 255 });
                sendDataSerial("b", new byte[] { 200 });
                brightnessIndex = 1;

                //fill keyboard
                myKeyboard.FillUpStatic();
            }
        }

        #region declaration

        //Time To fade Out
        private DateTime FadeOut;

        //contain the image name and shortcut path get he's data from file using "LoadFromFile" methode
        private List<Rectangle> ShortCutImages;

        private List<string> imagename;
        private List<string> ShortCut = new List<string>();

        //context menu
        private System.Windows.Forms.ContextMenuStrip CMS1;

        //Edite mode enable
        private bool EditeMode = false;

        //here we put the cureent  editing image
        private Rectangle EditingThisImage;

        // this timer for play StoryBoard when editeing mode Is enable
        private System.Windows.Forms.Timer TM1;

        //this timer for Clock
        private System.Windows.Forms.Timer ClockTimer;

        //store image name and pathe here for send it to edite mode
        private List<String> FullList = new List<string>();

        //performance monitor
        private PerformanceCounter cpuCounter;

        private PerformanceCounter ramCounter;
        private bool oneTimes = false;

        //to show edite message one time
        private bool ShowEditeMessage = true;

        private MyKeyboard myKeyboard;
        private Computer PC = new Computer();
        private double dpiX = 1;

        #endregion declaration

        #region DLLImport

        private object objShell = Type.Missing;

        [DllImport("user32.dll")]
        public static extern int ExitWindowsEx(int uFlags, int dwReason);

        [DllImport("user32.dll")]
        private static extern int SendMessage(int hWnd, int hMsg, int wParam, int lParam);

        public int WM_SYSCOMMAND = 0x0112;
        public int SC_MONITORPOWER = 0xF170; //Using the system pre-defined MSDN constants that can be used by the SendMessage() function .

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, RecycleFlags dwFlags);//recycle empty

        private enum RecycleFlags : uint
        {
            SHERB_NOCONFIRMATION = 0x00000001,
            SHERB_NOPROGRESSUI = 0x00000002,
            SHERB_NOSOUND = 0x00000004
        }

        #region Important for hide from alt + tab

        [Flags]
        public enum ExtendedWindowStyles
        {
            // ...
            WS_EX_TOOLWINDOW = 0x00000080,

            // ...
        }

        public enum GetWindowLongFields
        {
            // ...
            GWL_EXSTYLE = (-20),

            // ...
        }

        [DllImport("user32.dll")]
        public static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            int error = 0;
            IntPtr result = IntPtr.Zero;

            // Win32 SetWindowLong doesn't clear error on success
            SetLastError(0);

            if (IntPtr.Size == 4)
            {
                // use SetWindowLong
                Int32 tempResult = IntSetWindowLong(hWnd, nIndex, IntPtrToInt32(dwNewLong));
                error = Marshal.GetLastWin32Error();
                result = new IntPtr(tempResult);
            }
            else
            {
                // use SetWindowLongPtr
                result = IntSetWindowLongPtr(hWnd, nIndex, dwNewLong);
                error = Marshal.GetLastWin32Error();
            }

            if ((result == IntPtr.Zero) && (error != 0))
            {
                throw new System.ComponentModel.Win32Exception(error);
            }

            return result;
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr IntSetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern Int32 IntSetWindowLong(IntPtr hWnd, int nIndex, Int32 dwNewLong);

        private static int IntPtrToInt32(IntPtr intPtr)
        {
            return unchecked((int)intPtr.ToInt64());
        }

        [DllImport("kernel32.dll", EntryPoint = "SetLastError")]
        public static extern void SetLastError(int dwErrorCode);

        #endregion Important for hide from alt + tab

        #endregion DLLImport

        #region genralMethode

        //Form Load
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            PresentationSource source = PresentationSource.FromVisual(this);
            dpiX = 1;
            if (source != null)
            {
                dpiX = source.CompositionTarget.TransformToDevice.M11;
            }

            //Set Location at the middle of the screen
            this.Left = (System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / dpiX - this.Width) / 2;
            this.Top = -17;

            //Set Priority
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            //contex
            CMS1 = new System.Windows.Forms.ContextMenuStrip();
            CMS1.Items.Add("Change Image");
            CMS1.Items.Add("Change Directory");
            CMS1.ItemClicked += new System.Windows.Forms.ToolStripItemClickedEventHandler(CMS1_ItemClicked);

            //play storyboard timer
            TM1 = new System.Windows.Forms.Timer();
            TM1.Enabled = false;
            TM1.Interval = 800;
            TM1.Tick += new EventHandler(TM1_Tick);

            //Clock Timer to show clock
            ClockTimer = new System.Windows.Forms.Timer();
            ClockTimer.Enabled = true;
            ClockTimer.Interval = 1000;
            ClockTimer.Tick += new EventHandler(ClockTimer_Tick);

            //add image to image name list
            ShortCutImages.Add(image1);
            ShortCutImages.Add(image2);
            ShortCutImages.Add(image3);
            ShortCutImages.Add(image4);
            ShortCutImages.Add(image5);
            ShortCutImages.Add(image6);
            ShortCutImages.Add(image7);
            ShortCutImages.Add(image8);
            ShortCutImages.Add(image9);
            ShortCutImages.Add(image10);
            ShortCutImages.Add(image11);
            ShortCutImages.Add(image12);
            ShortCutImages.Add(image13);
            ShortCutImages.Add(image14);
            ShortCutImages.Add(image15);

            //read image name and shortcut path from setting file
            loadFromFile();

            //add image name and shortcut path to this list
            FullList.AddRange(imagename);
            FullList.AddRange(ShortCut);

            //load image from hard disk
            LoadImages();

            //hide theapplication from
            HideFromALTTab();

            //performance monitor
            cpuCounter = new PerformanceCounter();
            cpuCounter.CategoryName = "Processor";
            cpuCounter.CounterName = "% Processor Time";
            cpuCounter.InstanceName = "_Total";
            ramCounter = new PerformanceCounter("Memory", "Available MBytes");

            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;

            myKeyboard = new MyKeyboard();
            myKeyboard.FillUpStatic();

            PC.CPUEnabled = true;
            PC.GPUEnabled = true;
            PC.RAMEnabled = true;
            PC.Open();

            SystemEvents.SessionSwitch += new SessionSwitchEventHandler(SystemEvents_SessionSwitch);
        }

        //Send email with error
        private void SendERROR(String Message)
        {
            MessageBox.Show("Error here : " + Message);
        }

        //Key Pressed
        private void HotKeyManager_HotKeyPressed(object sender, HotKeyEventArgs e)
        {
            try
            {
                //Screen Captchre
                if (e.Key == System.Windows.Forms.Keys.S)
                {
                    image22_MouseLeftButtonUp(null, null);
                }

                //Show n Top
                else if (e.Key == System.Windows.Forms.Keys.X)
                {
                    this.Show();
                    ShowTop();

                    // locate position on the middele of screen
                    this.Left = (System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / dpiX - this.Width) / 2;
                    this.Top = -17;

                    if (DateTime.Now > FadeOut + new TimeSpan(0, 0, 8))
                    {
                        OpacityUp_BeginStoryboard1.Storyboard.Begin();
                    }

                    FadeOut = DateTime.Now;
                    myKeyboard.FillUpStatic();
                }

                //Screen Off
                else if (e.Key == System.Windows.Forms.Keys.O)
                {
                    image24_MouseLeftButtonUp(null, null);
                }

                //change Brightness
                else if (e.Key == System.Windows.Forms.Keys.B)
                {
                    image18_MouseLeftButtonUp(null, null);
                }

                //change color
                else if (e.Key == System.Windows.Forms.Keys.C)
                {
                    image19_MouseLeftButtonUp(null, null);
                }

                //change Mode
                else if (e.Key == System.Windows.Forms.Keys.M)
                {
                    image21_MouseLeftButtonUp(null, null);
                }

                //center mouse Cursor
                else if (e.Key == System.Windows.Forms.Keys.Q)
                {
                    SetPosition(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width / 2, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height / 2);
                }
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        private void SetPosition(int a, int b)
        {
            SetCursorPos(a, b);
        }

        [DllImport("User32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        //Clike on list
        private void CMS1_ItemClicked(object sender, System.Windows.Forms.ToolStripItemClickedEventArgs e)
        {
            if (EditeMode)
            {
                System.Windows.Forms.OpenFileDialog Z = new System.Windows.Forms.OpenFileDialog();
                System.Windows.Forms.FolderBrowserDialog c = new System.Windows.Forms.FolderBrowserDialog();
                Z.Title = "Chose photo ";
                Z.Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png";
                Z.Multiselect = false;

                try
                {
                    #region image1

                    if (EditingThisImage == image1)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[0] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[15] = c.SelectedPath;
                        }
                    }

                    #endregion image1

                    #region image2

                    if (EditingThisImage == image2)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[1] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[16] = c.SelectedPath;
                        }
                    }

                    #endregion image2

                    #region image3

                    if (EditingThisImage == image3)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[2] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[17] = c.SelectedPath;
                        }
                    }

                    #endregion image3

                    #region image4

                    if (EditingThisImage == image4)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[3] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[18] = c.SelectedPath;
                        }
                    }

                    #endregion image4

                    #region image5

                    if (EditingThisImage == image5)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[4] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[19] = c.SelectedPath;
                        }
                    }

                    #endregion image5

                    #region image6

                    if (EditingThisImage == image6)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[5] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[20] = c.SelectedPath;
                        }
                    }

                    #endregion image6

                    #region image7

                    if (EditingThisImage == image7)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[6] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[21] = c.SelectedPath;
                        }
                    }

                    #endregion image7

                    #region image8

                    if (EditingThisImage == image8)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[7] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[22] = c.SelectedPath;
                        }
                    }

                    #endregion image8

                    #region image9

                    if (EditingThisImage == image9)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[8] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[23] = c.SelectedPath;
                        }
                    }

                    #endregion image9

                    #region image10

                    if (EditingThisImage == image10)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[9] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[24] = c.SelectedPath;
                        }
                    }

                    #endregion image10

                    #region image11

                    if (EditingThisImage == image11)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[10] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[25] = c.SelectedPath;
                        }
                    }

                    #endregion image11

                    #region image12

                    if (EditingThisImage == image12)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[11] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[26] = c.SelectedPath;
                        }
                    }

                    #endregion image12

                    #region image13

                    if (EditingThisImage == image13)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[12] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[27] = c.SelectedPath;
                        }
                    }

                    #endregion image13

                    #region image14

                    if (EditingThisImage == image14)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[13] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[28] = c.SelectedPath;
                        }
                    }

                    #endregion image14

                    #region image15

                    if (EditingThisImage == image15)
                    {
                        if (e.ClickedItem.Text == "Change Image" && Z.ShowDialog() == System.Windows.Forms.DialogResult.OK && Z.FileName != String.Empty)
                        {
                            FullList.Add(Z.FileName);
                            FullList[14] = System.IO.Path.GetFileName(Z.FileName);
                        }
                        else if (e.ClickedItem.Text == "Change Directory" && c.ShowDialog() == System.Windows.Forms.DialogResult.OK && c.SelectedPath != String.Empty)
                        {
                            FullList[29] = c.SelectedPath;
                        }
                    }

                    #endregion image15

                    if (ShowEditeMessage)
                    {
                        MessageBox.Show("Changes Will Take Effects After Disable Editing Mode", "Wooow ShortCuts", MessageBoxButton.OK, MessageBoxImage.Information);
                        ShowEditeMessage = false;
                    }
                }
                catch (Exception ER)
                {
                    SendERROR(ER.Message);
                }
            }
        }

        //this method use at startup to hide the app from alt and tab
        private void HideFromALTTab()
        {
            try
            {
                WindowInteropHelper wndHelper = new WindowInteropHelper(this);

                int exStyle = (int)GetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE);

                exStyle |= (int)ExtendedWindowStyles.WS_EX_TOOLWINDOW;
                SetWindowLong(wndHelper.Handle, (int)GetWindowLongFields.GWL_EXSTYLE, (IntPtr)exStyle);
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        #endregion genralMethode

        #region FilesEdite

        //read image name and shortcut path
        private void loadFromFile()
        {
            try
            {
                using (StreamReader Read = new StreamReader("Setting.SSC"))
                {
                    for (int i = 0; i < 15; i++)
                    {
                        imagename.Add(Read.ReadLine());
                    }

                    for (int i = 0; i < 15; i++)
                    {
                        ShortCut.Add(Read.ReadLine());
                    }
                }
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
                MessageBox.Show("ERROR" + Environment.NewLine + "Can't Access To Necessary File ." + Environment.NewLine +
                "Try To Restart Your PC , If The Problem Continue Reinstall The Wooow ShortCut ", "Wooow ShortCuts", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        //Load Images
        private void LoadImages()
        {
            for (int i = 0; i < 15; i++)
            {
                try
                {
                    ImageSourceConverter converter = new ImageSourceConverter();
                    ImageBrush c = new ImageBrush((ImageSource)converter.ConvertFromString(@"images/" + imagename[i]));
                    ShortCutImages[i].Fill = c;
                }
                catch
                {
                    ImageSourceConverter converter = new ImageSourceConverter();
                    ImageBrush c = new ImageBrush((ImageSource)converter.ConvertFromString(@"images/ERRORIMAGE.png"));
                    ShortCutImages[i].Fill = c;
                }
            }
        }

        #endregion FilesEdite

        #region Timers

        //Timer Draw Clock and cpu and free ram
        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (oneTimes)
                {
                    oneTimes = false;
                    label2.Content = (int)cpuCounter.NextValue() + "%";
                    label3.Content = ramCounter.NextValue() + "MB";

                    label2_Copy.Content = "";
                    label3_Copy.Content = "";

                    //get gpu load
                    for (int i = 0; i < PC.Hardware.Length; i++)
                    {
                        if (PC.Hardware[i].HardwareType == OpenHardwareMonitor.Hardware.HardwareType.GpuNvidia)
                        {
                            PC.Hardware[i].Update();
                            for (int j = 0; j < PC.Hardware[i].Sensors.Length; j++)
                            {
                                if (PC.Hardware[i].Sensors[j].SensorType == OpenHardwareMonitor.Hardware.SensorType.Temperature)
                                {
                                    label3_Copy.Content += PC.Hardware[i].Sensors[j].Value + "°c";
                                }
                                else if (PC.Hardware[i].Sensors[j].SensorType == OpenHardwareMonitor.Hardware.SensorType.Load && PC.Hardware[i].Sensors[j].Name.Contains("Core"))
                                {
                                    label2_Copy.Content += (int)Math.Round((double)(PC.Hardware[i].Sensors[j].Value)) + "%";
                                }
                            }
                        }
                    }
                }
                else
                {
                    oneTimes = true;
                }
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        //Story Board Timer
        private void TM1_Tick(object sender, EventArgs e)
        {
            try
            {
                //play Story Board Now
                EditeModeStoryBoard_BeginStoryboard.Storyboard.Begin();
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        #endregion Timers

        #region Control Image Clicked

        //Windows Restart
        private void image16_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            List<string> dirs = new List<string>()
            {
                System.IO.Path.GetTempPath(),
                @"C:\Windows\Temp",
                @"C:\Windows\Prefetch"
            };

            foreach (var dir in dirs)
            {
                try
                {
                    foreach (var item in Directory.GetFiles(dir))
                    {
                        try { File.Delete(item); } catch { }
                    }
                }
                catch { }

                try
                {
                    foreach (var item in Directory.GetDirectories(dir))
                    {
                        try { Directory.Delete(item, true); } catch { }
                    }
                }
                catch { }
            }

            try { SHEmptyRecycleBin(IntPtr.Zero, null, 0); }
            catch { }

            MessageBox.Show("Temp Folder Clear Successfully", "Successfully", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        //Windows ShutDown
        private void image17_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MessageBoxResult.OK == MessageBox.Show("Your Computer Will ShutDown ...", "Wooow ShortCuts", MessageBoxButton.OKCancel, MessageBoxImage.Question))
                try
                {
                    ManagementBaseObject mboShutdown = null;
                    ManagementClass mcWin32 = new ManagementClass("Win32_OperatingSystem");
                    mcWin32.Get();
                    mcWin32.Scope.Options.EnablePrivileges = true;
                    ManagementBaseObject mboShutdownParams = mcWin32.GetMethodParameters("Win32Shutdown");
                    mboShutdownParams["Flags"] = "1";
                    mboShutdownParams["Reserved"] = "0";
                    foreach (ManagementObject manObj in mcWin32.GetInstances())
                    {
                        mboShutdown = manObj.InvokeMethod("Win32Shutdown",
                                                       mboShutdownParams, null);
                    }
                }
                catch (Exception x)
                {
                    SendERROR(x.Message);
                }
        }

        //Change Brightness
        private int brightnessIndex = 1;

        private void image18_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //emulator 1

            Process cmd = new Process();
            cmd.StartInfo.FileName = @"C:\Users\as206\Desktop\Desktop_Projects\_Scripts\RoxiitEmulators\RunFirstEmulator.bat";
            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            cmd.StartInfo.Arguments = "";
            cmd.Start();
        }

        //Change Color
        private int colorIndex = 1;

        private void image19_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //emulator 2

            Process cmd = new Process();
            cmd.StartInfo.FileName = @"C:\Users\as206\Desktop\Desktop_Projects\_Scripts\RoxiitEmulators\RunSecondEmulator.bat";
            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            cmd.StartInfo.Arguments = "";
            cmd.Start();
        }

        //Color Mode
        private int colorMode = 1;

        private void image21_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            //slack

            Process cmd = new Process();
            cmd.StartInfo.FileName = @"C:\Users\as206\Desktop\Desktop_Projects\_Scripts\Slack\ClearSlack.bat";
            cmd.StartInfo.Arguments = "";
            cmd.Start();
        }

        //screen captcher
        private void image22_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                System.Drawing.Bitmap bmpScreenshot;
                System.Drawing.Graphics gfxScreenshot;
                bmpScreenshot = new System.Drawing.Bitmap(System.Windows.Forms.Screen.PrimaryScreen.Bounds.Width, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Height); // PixelFormat.Format32bppArgb
                gfxScreenshot = System.Drawing.Graphics.FromImage(bmpScreenshot);
                gfxScreenshot.CopyFromScreen(System.Windows.Forms.Screen.PrimaryScreen.Bounds.X, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Y, 0, 0, System.Windows.Forms.Screen.PrimaryScreen.Bounds.Size, System.Drawing.CopyPixelOperation.SourceCopy);
                bmpScreenshot.Save(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + @"\" + "SniperShortCut" + DateTime.Now.Second + DateTime.Now.Millisecond + ".png");
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        //Hide applicatio
        private void image23_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                this.Hide();
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        //screen off and turn led off
        private void image24_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process cmd = new Process();
                cmd.StartInfo.FileName = @"C:\Users\as206\Desktop\Desktop_Projects\_Scripts\ConvertVideos\ConverDownloadsFolder.bat";
                cmd.StartInfo.Arguments = "";
                cmd.Start();
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        //Edite Mode
        private bool isShutDownByEdite = false;

        private void image20_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            try
            {
                EditeMode = !EditeMode;
                if (EditeMode)
                {
                    TM1.Start();
                    MessageBox.Show("Edite Mode Enable", "Wooow ShortCuts", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    TM1.Stop();
                    MessageBox.Show("Edite Mode Disable", "Wooow ShortCuts", MessageBoxButton.OK, MessageBoxImage.Information);
                    String kl = "15";
                    foreach (var item in FullList)
                    {
                        kl += ";";
                        kl += item;
                    }
                    isShutDownByEdite = true;
                    Process.Start("WoowEdit.exe", kl);
                    Application.Current.Shutdown();
                    ShowEditeMessage = true;
                }
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        #endregion Control Image Clicked

        #region ShortCut images Clicked

        private void images_MouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            Rectangle X = (Rectangle)sender;

            #region normalClick

            if (!EditeMode)
            {
                try
                {
                    if (X == image1)
                    {
                        Process.Start(ShortCut[0]);
                    }
                    else if (X == image2)
                    {
                        Process.Start(ShortCut[1]);
                    }
                    else if (X == image3)
                    {
                        Process.Start(ShortCut[2]);
                    }
                    else if (X == image4)
                    {
                        Process.Start(ShortCut[3]);
                    }
                    else if (X == image5)
                    {
                        Process.Start(ShortCut[4]);
                    }
                    else if (X == image6)
                    {
                        Process.Start(ShortCut[5]);
                    }
                    else if (X == image7)
                    {
                        Process.Start(ShortCut[6]);
                    }
                    else if (X == image8)
                    {
                        Process.Start(ShortCut[7]);
                    }
                    else if (X == image9)
                    {
                        Process.Start(ShortCut[8]);
                    }
                    else if (X == image10)
                    {
                        Process.Start(ShortCut[9]);
                    }
                    else if (X == image11)
                    {
                        Process.Start(ShortCut[10]);
                    }
                    else if (X == image12)
                    {
                        Process.Start(ShortCut[11]);
                    }
                    else if (X == image13)
                    {
                        Process.Start(ShortCut[12]);
                    }
                    else if (X == image14)
                    {
                        Process.Start(ShortCut[13]);
                    }
                    else if (X == image15)
                    {
                        Process.Start(ShortCut[14]);
                    }
                }
                catch
                {
                    if (MessageBox.Show("you didn't choose any file or path to Start !" + Environment.NewLine + "enable edit mode to select one ?", "Wooow ShortCuts", MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK)
                    {
                        image20_MouseLeftButtonUp(null, null);
                    }
                }
            }

            #endregion normalClick

            #region Editemode

            if (EditeMode)
            {
                try
                {
                    if (X == image1)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image1.Margin.Left;
                        Temp.Y += image1.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image2)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image2.Margin.Left;
                        Temp.Y += image2.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image3)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image3.Margin.Left;
                        Temp.Y += image3.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image4)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image4.Margin.Left;
                        Temp.Y += image4.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image5)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image5.Margin.Left;
                        Temp.Y += image5.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image6)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image6.Margin.Left;
                        Temp.Y += image6.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image7)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image7.Margin.Left;
                        Temp.Y += image7.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image8)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image8.Margin.Left;
                        Temp.Y += image8.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image9)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image9.Margin.Left;
                        Temp.Y += image9.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image10)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image10.Margin.Left;
                        Temp.Y += image10.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image11)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image11.Margin.Left;
                        Temp.Y += image11.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image12)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image12.Margin.Left;
                        Temp.Y += image12.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image13)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image13.Margin.Left;
                        Temp.Y += image13.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image14)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image14.Margin.Left;
                        Temp.Y += image14.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }
                    else if (X == image15)
                    {
                        Point Temp = new Point(this.Left + 30, this.Top + 40);
                        Temp.X += image15.Margin.Left;
                        Temp.Y += image15.Margin.Top;
                        CMS1.Show((int)Temp.X, (int)Temp.Y);
                    }

                    EditingThisImage = X;
                }
                catch (Exception x)
                {
                    SendERROR(x.Message);
                }
            }

            #endregion Editemode
        }

        private void images_MouseRightUp(object sender, MouseButtonEventArgs e)
        {
        }

        #endregion ShortCut images Clicked

        #region StoryBoardStart

        //Start StoryBoard when mouse leave
        private void Window1_MouseLeave(object sender, MouseEventArgs e)
        {
            try
            {
                if (DateTime.Now != FadeOut)
                {
                    OpatcityDown_BeginStoryboard.Storyboard.Begin();
                }
            }
            catch (Exception v)
            {
                SendERROR(v.Message);
            }
        }

        //Start StoryBoard when mouse enter
        private void Window1_MouseEnter(object sender, MouseEventArgs e)
        {
            try
            {
                if (DateTime.Now > FadeOut + new TimeSpan(0, 0, 8))
                {
                    OpacityUp_BeginStoryboard1.Storyboard.Begin();
                }

                FadeOut = DateTime.Now;
            }
            catch (Exception v)
            {
                SendERROR(v.Message);
            }
        }

        //use thos metode to show the app on the screen
        private void ShowTop()
        {
            try
            {
                Window1.Topmost = true;

                Window1.Topmost = false;
            }
            catch (Exception x)
            {
                SendERROR(x.Message);
            }
        }

        #endregion StoryBoardStart

        //form closing
        private void Window1_Closing(object sender, CancelEventArgs e)
        {
            if (!isShutDownByEdite)
            {
                if (MessageBox.Show("Do you want to close this app?", "Are you sure", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.No)
                    e.Cancel = true;
            }
        }

        private void sendDataSerial(string command, byte[] data)
        {
            try
            {
                using (SerialPort port = new SerialPort("COM3", 9600))
                {
                    port.Open();
                    port.WriteLine(command);
                    port.Write(data, 0, data.Length);
                }
            }
            catch { }
        }

        private void Window1_MouseMove(object sender, MouseEventArgs e)
        {
            FadeOut = DateTime.Now;
        }
    }
}