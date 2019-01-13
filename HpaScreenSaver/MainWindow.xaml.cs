using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace HpaScreenSaver
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Timer timer;
        private bool IsSwitching = false;
        private HpaClient.ImageInfo nextImage = null;

        public HpaClient.ImageInfo Image
        {
            get { return (HpaClient.ImageInfo)GetValue(ImageProperty); }
            set { SetValue(ImageProperty, value); }
        }
        
        public static readonly DependencyProperty ImageProperty =
            DependencyProperty.Register("Image", typeof(HpaClient.ImageInfo), typeof(MainWindow), new PropertyMetadata(null));



        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;         
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {            
            DataContext = this;
            Mouse.OverrideCursor = Cursors.None;
            WindowState = WindowState.Maximized;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //MessageBox.Show("Shutting down");
            Application.Current.Shutdown();
            //Environment.Exit(0);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            //MessageBox.Show("Shutting down");
            Application.Current.Shutdown();
            //Environment.Exit(0);
        }

        public void StartShow() {            
            timer = new Timer();
            timer.Interval = 10000;
            timer.AutoReset = true;
            timer.Elapsed += Timer_Elapsed;
            timer.Start();

            IsSwitching = true;
            try
            {
                Image = HpaClient.getRandImageTask().Result;
                nextImage = HpaClient.getRandImageTask().Result;            
            }
            catch (Exception ex)
            {
                MessageBox.Show("first init exception: " + ex.ToString());
            }
            finally
            {
                IsSwitching = false;
            }
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {            
            // System.Diagnostics.Trace.WriteLine("tick...");
            if (!IsSwitching) {
                IsSwitching = true;
                Task.Run(() => {
                    try
                    {
                        Dispatcher.Invoke(() => { Image = nextImage; });
                        nextImage = HpaClient.getRandImageTask().Result;
                    }
                    catch (Exception ex) {
                        MessageBox.Show("Exception: " + ex.ToString());
                    }
                    finally
                    {
                        IsSwitching = false;
                    }

                });                
            }
        }
    }
}
