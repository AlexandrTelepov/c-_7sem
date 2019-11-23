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

using Microsoft.Win32;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;

using RecognitionLib;

using System.Data.Entity;

namespace Task2
{
    public class DirClass : INotifyPropertyChanged
    {
        private string Dir_path;
        public string dir_path { get { return Dir_path; } set { Dir_path = value; OnPropertyChanged("dir_path"); } }
        public DirClass(string dir_path)
        {
            this.dir_path = dir_path;
        }
        public override string ToString()
        {
            return dir_path;
        }
        public event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
            }
        }
    }
    public partial class MainWindow : Window
    {

        public All_Results results = new All_Results();
        public string model_path = "D:\\c#\\7_sem\\1_task\\models\\mnist\\mnist\\model.onnx";
        public DirClass dir_class = new DirClass("");
        public int class_num = 0;

        public delegate void NextPrimeDelegate();
        public bool is_executed { get; set; }
        public bool Is_changed = false;
        CancellationTokenSource cancelTokenSource;
        CancellationToken token;

        public MainWindow()
        {
            InitializeComponent();

            is_executed = false;
            Dir.DataContext = dir_class;
            Results.ItemsSource = results.images_with_classes;
            Classes.ItemsSource = results.classes;
 
        }

        public static RoutedCommand StartComand = new RoutedCommand("Start", typeof(Task2.MainWindow));

        private void StartCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            results = new All_Results();
            Results.ItemsSource = results.images_with_classes;
            Classes.ItemsSource = results.classes;

            CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
            this.cancelTokenSource = cancelTokenSource;
            this.token = cancelTokenSource.Token;
            is_executed = true;

            RecognitionClass rec = new RecognitionClass(dir_class.dir_path, model_path, results, token, cancelTokenSource);
            BindingOperations.EnableCollectionSynchronization(results.classes, rec.waitHandler);
            BindingOperations.EnableCollectionSynchronization(results.images_with_classes, rec.waitHandler);
            for (int i =0; i < 10; i++)
            {
                BindingOperations.EnableCollectionSynchronization(results.results[i], rec.waitHandler);
            }

            //MessageBox.Show(rec.LoadJpg("D:\\data\\1\\2.jpg")[0].ToString());
            //MessageBox.Show(rec.LoadJpg("D:\\data\\1\\5.jpg").ToString());
            //using (UserContext db = new UserContext())
            //{
            //    db.images_with_names.Add(new images_with_names("D:\\data\\1\\5.jpg", rec.LoadJpg("D:\\data\\1\\5.jpg")));
            //    db.SaveChanges();
            //}
            //return;
            Task.Run(() => rec.Recognize());
            Is_changed = true;
            is_executed = false;
        }



        private void CanStartCommandHandler(object sender, CanExecuteRoutedEventArgs e)
        {
            if (!is_executed && (dir_class.dir_path != ""))
            {
                e.CanExecute = true;
            }
            else
            {
                e.CanExecute = false;
            }
        }

        public static RoutedCommand StopComand = new RoutedCommand("Stop", typeof(Task2.MainWindow));
        private void StopCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
            is_executed = false;
        }

 

        public static RoutedCommand ChooseComand = new RoutedCommand("Stop", typeof(Task2.MainWindow));
        private void ChooseCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                System.Windows.Forms.DialogResult result = dialog.ShowDialog();
                dir_class.dir_path = dialog.SelectedPath;
                MessageBox.Show(dir_class.dir_path);
            }
        }

        public static RoutedCommand ClearComand = new RoutedCommand("Clear", typeof(Task2.MainWindow));
        private void ClearCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            using (UserContext db = new UserContext())
            {
                db.Database.ExecuteSqlCommand("TRUNCATE TABLE [Table]");
                db.Database.ExecuteSqlCommand("TRUNCATE TABLE [TableImages]");
                db.SaveChanges();
            }
        }

        public static RoutedCommand GetStatComand = new RoutedCommand("Clear", typeof(Task2.MainWindow));
        private void GetStatCommandHandler(object sender, ExecutedRoutedEventArgs e)
        {
            if (Classes.SelectedItem != null)
            {
                using (UserContext db = new UserContext())
                {
                    int res_class = (int) Classes.SelectedItem;
                    int count = db.images_with_classes.Where(p => p.res_class == res_class).Count();
                    Stat.Text = count.ToString();
                }
            }
        }


        public FileStream fs = null;

        private void SaveDialog()
        {
            MessageBoxResult result = MessageBox.Show(
                    "Do you want save current results?",
                    "Message",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information,
                    MessageBoxResult.Yes,
                    MessageBoxOptions.DefaultDesktopOnly);
            if (result == MessageBoxResult.Yes)
            {
                SaveFileDialog dialog = new SaveFileDialog();
                if (dialog.ShowDialog() == true)
                {
                    string file_name = dialog.FileName;
                    try
                    {
                        BinaryFormatter formatter = new BinaryFormatter();
                        fs = File.Open(file_name + ".bin", FileMode.OpenOrCreate);
                        formatter.Serialize(fs, this.results);
                        fs.Close();
                        Is_changed = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        Console.WriteLine(ex.StackTrace);
                    }
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (Is_changed == true)
            {
                SaveDialog();
            }
        }

        private void listbox_selection_changed(object sender, SelectionChangedEventArgs e)
        {
            if (Classes.SelectedItem != null && results.results[(int)(Classes.SelectedItem)] != null)
            {
                All.ItemsSource = results.results[(int)(Classes.SelectedItem)];


                Count.DataContext = results.count[(int)(Classes.SelectedItem)];
                Binding binding2 = new Binding() { Path = new PropertyPath("Count") };
                binding2.Mode = BindingMode.OneWay;
                Count.SetBinding(TextBox.TextProperty, binding2);
            }
        }
    }
}
