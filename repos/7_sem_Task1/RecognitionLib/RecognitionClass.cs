using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using NN_Inference;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Data.Entity;

using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Drawing;

namespace RecognitionLib
{
    [Table("Table")]
    public class images_with_classes
    {
        public int res_class { get; set; }
        [Key]
        public string file_name { get; set; }
        public string suffix { get; set; }
        public float prob { get; set; }
        public images_with_classes()
        {
        }
        public images_with_classes(int res_class, string file_name, float prob, string suffix)
        {
            this.res_class = res_class;
            this.file_name = file_name;
            this.prob = prob;
            this.suffix = suffix;
        }
    }
    [Table("TableImages")]
    public class images_with_names
    {
        [Key]
        public string file_name { get; set; }
        public byte[] image { get; set; } = new byte[1*28*28];
        public images_with_names()
        {
        }
        public images_with_names(string file_name, byte[] image)
        {
            this.file_name = file_name;
            this.image = image;
        }
    }

    public class CountClass: INotifyPropertyChanged
    {
        private int count;
        public event PropertyChangedEventHandler PropertyChanged = delegate { };
        public CountClass()
        {
            this.count = 0;
        }
        public int Count
        {
            get { return this.count; }
            set
            {
                this.count = value;
                this.OnPropertyChanged();
            }
        }
        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }
        public override string ToString()
        {
            return count.ToString();
        }
    }
    [Serializable]
    public class All_Results
    {
        public Dictionary<int, ObservableCollection<string>> results;
        public ObservableCollection<int> classes;
        public ObservableCollection<images_with_classes> images_with_classes;
        public Dictionary<int, CountClass> count;
        public All_Results()
        {
            results = new Dictionary<int, ObservableCollection<string>>();
            classes = new ObservableCollection<int>();
            images_with_classes = new ObservableCollection<images_with_classes>();
            count = new Dictionary<int, CountClass>();
            for (int i = 0; i < 10; i++)
            {
                count.Add(i, new CountClass());
                results.Add(i, new ObservableCollection<string>(new List<string>()));
            }
        }
    }

    public class RecognitionClass
    {

        string dir_path;
        string model_path;
        All_Results all_results;
        public AutoResetEvent waitHandler = new AutoResetEvent(true);
        CancellationToken token;
        CancellationTokenSource cancelTokenSource;

        public RecognitionClass(string dir_path, string model_path,
            All_Results all_results, CancellationToken token, CancellationTokenSource cancelTokenSource)
        {
            this.dir_path = dir_path;
            this.model_path = model_path;
            this.all_results = all_results;
            this.token = token;
            this.cancelTokenSource = cancelTokenSource;
        }

        public void Recognize()
        {

            Inferencer inferencer = new Inferencer(dir_path, model_path, token, all_results, waitHandler);

            int num_files = new DirectoryInfo(dir_path).GetFiles().Length;

            var po = new ParallelOptions { CancellationToken = token };
            var tasks = new Task[num_files];
            try
            {
                for (int i = 0; i < num_files; i++)
                {
                    tasks[i] = Task.Factory.StartNew(pi =>
                    {
                        bool in_db = false;
                        int idx = (int)pi;
                        byte[] img_cur;
                        using (UserContext db = new UserContext())
                        {
                            string[] split_str_cur = inferencer.files[idx].Split('\\');
                            string suffix_cur = split_str_cur[split_str_cur.Length - 1];

                            var querry_result = db.images_with_classes.Where(p => p.suffix == suffix_cur);
                            if (querry_result.Count() != 0)
                            {
                                string img_cur_name = inferencer.files[idx];
                                img_cur = LoadJpg(img_cur_name);
                                foreach (var item in querry_result.ToList())
                                {
                                    var item_img = db.images_with_names.Find(item.file_name).image;
                                    if (item_img.SequenceEqual(img_cur))
                                    {
                                        in_db = true;
                                        var res_class = item.res_class;
                                        var prob = item.prob;
                                        waitHandler.WaitOne();
                                        {
                                            all_results.results[res_class].Add(item.file_name);
                                            all_results.count[res_class].Count += 1;
                                            if (!all_results.classes.Contains(res_class))
                                            {
                                                all_results.classes.Add(res_class);
                                            }
                                            string[] split_sample_name = item.file_name.Split('\\');
                                            string suffix = split_sample_name[split_sample_name.Length - 1];
                                            images_with_classes img_obj = new images_with_classes(res_class, item.file_name, prob, suffix);
                                            all_results.images_with_classes.Add(img_obj);
                                        }
                                        waitHandler.Set();
                                        break;
                                    }
                                }
                            }
                        }
                        if (!in_db)
                        {
                            inferencer.CalcInference(idx);
                            using (UserContext db = new UserContext())
                            {
                                db.images_with_names.Add(new images_with_names(inferencer.files[idx], LoadJpg(inferencer.files[idx])));
                                db.SaveChanges();
                            }
                        }
                    }, i);
                }
            }
            catch (OperationCanceledException ex)
            {
                Console.WriteLine("Операция прервана");
            }
            finally
            {
                Task.WaitAll(tasks);
                cancelTokenSource.Dispose();
            }
        }

        public byte[] LoadJpg(string img_path)
        {
            Bitmap img = Image.FromFile(img_path) as Bitmap;
            byte[] inputData = new byte[1 * 28 * 28];
            for (int i = 0; i < img.Width; i++)
            {
                for (int j = 0; j < img.Height; j++)
                {
                    Color pixel = img.GetPixel(i, j);
                    inputData[i * img.Height + j] = pixel.R;
                }
            }
            return inputData;
        }
    }

    class Inferencer
    {
        string dir_path;
        string model_path;
        CancellationToken token;
        public string[] files;
        All_Results all_results;
        AutoResetEvent waitHandler;

        public Inferencer(string dir_path, string model_path, CancellationToken token,
            All_Results all_results, AutoResetEvent waitHandler)
        {
            this.dir_path = dir_path + "\\";
            this.model_path = model_path;
            this.token = token;
            this.files = (Directory.GetFiles(dir_path));
            this.all_results = all_results;
            this.waitHandler = waitHandler;
        }

        public void CalcInference(int idx)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            string sample_name = files[idx];
            NN_Inferencer inference_obj = new NN_Inferencer(model_path, sample_name);
            float[] result = inference_obj.forward();
            int res_class = 0;
            float res_score = -10000;
            double e = Math.E;
            double sum_exp = 0;
            for (int j = 0; j < result.Length; j++)
            {
                sum_exp += Math.Pow(e, result[j]);
                if (result[j] > res_score)
                {
                    res_score = result[j];
                    res_class = j;
                }
            }
            float res_prob = (float)(Math.Pow(e, res_score) / sum_exp);

            waitHandler.WaitOne();
            {
                all_results.results[res_class].Add(sample_name);
                all_results.count[res_class].Count += 1;
                if (!all_results.classes.Contains(res_class))
                {
                    all_results.classes.Add(res_class);
                }
                string[] split_sample_name = sample_name.Split('\\');
                string suffix = split_sample_name[split_sample_name.Length - 1];
                images_with_classes img_obj = new images_with_classes(res_class, sample_name, res_prob, suffix);
                all_results.images_with_classes.Add(img_obj);

                using (UserContext db = new UserContext())
                {
                    db.images_with_classes.Add(img_obj);
                    db.SaveChanges();
                }
            }
            
            waitHandler.Set();
        }
    }

    public class UserContext : DbContext
    {
        public UserContext() :
            base("UserDB")
        { }

        public DbSet<images_with_classes> images_with_classes { get; set; }
        public DbSet<images_with_names> images_with_names { get; set; }
    }
}

