using LumasharpChecker.viewmodel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LumasharpChecker
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Version? version = Assembly.GetEntryAssembly().GetName().Version;
            this.Title += $" {version?.ToString()}";
        }
        //[DllImport("user32.dll")]
        //private static extern bool SetForegroundWindow(IntPtr hWnd);
    }
}
