using System;
using System.Text;
using System.Windows;
using ConsoleApplication1.Manager;

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
            this.Dispatcher.Invoke((Action)delegate
            {
                resultBlock.Text = "->";
                StopButton.IsEnabled = true;
            });
            
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
            
            this.Dispatcher.Invoke((Action)delegate
            {
                StopButton.IsEnabled = false;
            });

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
