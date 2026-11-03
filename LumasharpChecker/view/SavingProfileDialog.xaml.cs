using System;
using System.Collections.Generic;
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

namespace LumasharpChecker.view
{
    /// <summary>
    /// Interaction logic for SavingProfileDialog.xaml
    /// </summary>
    public partial class SavingProfileDialog : Window
    {
        public string newPorfileName;
        public SavingProfileDialog()
        {
            InitializeComponent();
        }
#warning 應該使用MVVM架構!
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            newPorfileName = TBNewProfileName.Text;
            if(string.IsNullOrEmpty(newPorfileName))
            {
                MessageBox.Show("沒有填入Profile Name");
                return;
            }

            DialogResult = true;
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
