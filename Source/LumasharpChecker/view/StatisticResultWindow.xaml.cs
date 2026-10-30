using LumasharpChecker.viewmodel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LumasharpChecker.model;

namespace LumasharpChecker.view
{
    /// <summary>
    /// Interaction logic for StasticResultWindow.xaml
    /// </summary>
    public partial class StatisticResultWindow : Window
    {
        public StatisticResultWindow(StatisticResultModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            //this.DataContext = ProfileManager.Instance;
        }
        //public static InverseBooleanConverter InverseBoolConverter { get; } = new InverseBooleanConverter();


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            Close();
        }
    }
    //public class InverseBooleanConverter : IValueConverter
    //{
    //    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is bool b)
    //            return !b;
    //        return value;
    //    }

    //    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    //    {
    //        if (value is bool b)
    //            return !b;
    //        return value;
    //    }
    //}
}
