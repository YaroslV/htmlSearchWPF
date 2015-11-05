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
using System.Windows.Navigation;
using System.Windows.Shapes;
using ConsoleApplication1.Manager;
using System.Collections.Concurrent;

namespace WpfTestTask
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private HtmlSearchManager htmlSearchManager;
        private StringBuilder outputStringBuilder = new StringBuilder();        
        

        public MainWindow()
        {
            InitializeComponent();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            resultBlock.Text = "";
            //TODO 
            //write checking for input parameters 
            int threadNumber = 1;
            int.TryParse(threadNum.Text, out threadNumber);
            int urlNumber = 10;
            int.TryParse(urlNum.Text, out urlNumber);

            //init hrml searcher            
            htmlSearchManager = new HtmlSearchManager(inputUrlTxtBox.Text, textToSearchTxtBox.Text,
                urlNumber, threadNumber, WriteToResultTextBox);
            htmlSearchManager.StartSearch();
        }

        //output method
        private void WriteToResultTextBox(string text)
        {
            outputStringBuilder.Append(text);
            
            this.Dispatcher.Invoke((Action)delegate
            {
                resultBlock.Text = outputStringBuilder.ToString();
            });
            
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            htmlSearchManager.StopSearch();
            resultBlock.Text = "Execution stopped...";
        }
    }
}
