using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;

namespace PWpfLauncher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        bool _shown;
        private string _appname;
        private string _path="";
        private string _exeName;
        private int _totalFile;
        private bool _pb1 = true;
        private bool _pb2 = true;
        private List<Hasil> listRemote;
        public MainWindow()
        {
            _appname = "TES";
            _path = AppDomain.CurrentDomain.BaseDirectory + "tes";
            _exeName = "application.exe";
            InitializeComponent();
        }
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_shown)
                return;

            _shown = true;
            getUpdate();
            // Your code here.
        }

        private async void getUpdate()
        {
            lbl_total.Content = "Checking for update";
            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "C# console program");

            Dictionary<string,string> param = new Dictionary<string, string>
            {
                {"appname",_appname }
            };
            FormUrlEncodedContent paramCode = new FormUrlEncodedContent(param);
            HttpResponseMessage resultAsync = await client.PostAsync("https://blog.setiawan.co.id/AppUpdate/appCheck.php",paramCode);
            string content = await resultAsync.Content.ReadAsStringAsync();
            compareFile(content);
        }

        private void compareFile( string valJson)
        {
            Directory.CreateDirectory(_path);
            JavaScriptSerializer js = new JavaScriptSerializer();
            listRemote = js.Deserialize<List<Hasil>>(valJson);
            List<string> listFiles = Directory.GetFiles(_path, "*.*", SearchOption.AllDirectories).OrderBy(p => p).ToList();
            MD5 totMd5 = MD5.Create();
            foreach (string files in listFiles)
            {
                for (int i = 0; i < listRemote.Count; i++)
                {
                    string cPath = Path.GetFileName(files);
                    string cUrl = HttpUtility.ParseQueryString(new Uri(listRemote[i].alamat).Query).Get("filenya");
                    if (cPath.Equals(cUrl))
                    {
                        if (GetMD5HashFromFile(files).ToLower().Equals(listRemote[i].checksum))
                        {
                            listRemote.RemoveAt(i);
                            break;
                        }
                    }
                }
            }

            _totalFile = listRemote.Count;
            Dispatcher.Invoke(() =>
            {
                pb_total.Maximum = _totalFile;
                lbl_total.Content = "Downloading: " + 0 + " / " + _totalFile;
            });
            new Thread((() => downAct())).Start();
            new Thread((() => downAct())).Start();
        }

        private string GetMD5HashFromFile(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", string.Empty);
                }
            }
        }

        private static string md5_file(string fileName)
        {
            FileStream file = new FileStream(fileName, FileMode.Open);
            MD5 md5 = new MD5CryptoServiceProvider();
            int length = (int)file.Length;  // get file length
            byte[] buffer = new byte[length];      // create buffer
            int count;                      // actual number of bytes read
            int sum = 0;                    // total number of bytes read

            // read until Read method returns 0 (end of the stream has been reached)
            while ((count = file.Read(buffer, sum, length - sum)) > 0)
                sum += count;  // sum is a buffer offset for next reading
            byte[] retVal = md5.ComputeHash(buffer);
            file.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }

        private async void downAct()
        {
            if (listRemote.Count == 0 && _pb2 && _pb1)
            {
                /*Process p = new Process();
                p.StartInfo = new ProcessStartInfo(AppDomain.CurrentDomain.BaseDirectory + _path + @"\" + _exeName);
                p.Start();*/
                Dispatcher.Invoke(() => { Close(); });
            }
            string url = "";
            ProgressBar pb = null;
            Label lb = null;
            int pbid = 0;
            if (_pb1 && listRemote.Count > 0)
            {
                url = listRemote[0].alamat;
                listRemote.RemoveAt(0);
                _pb1 = false;
                pbid = 1;
                pb = pb_1;
                lb = lbl_file1;
            }
            else if(_pb2 && listRemote.Count>0)
            {
                url = listRemote[listRemote.Count - 1].alamat;
                listRemote.RemoveAt(listRemote.Count - 1);
                _pb2 = false;
                pbid = 2;
                pb = pb_2;
                lb = lbl_file2;
            }
            if(pb != null)
            {
                Uri myUri = new Uri(url);
                string fileName = HttpUtility.ParseQueryString(myUri.Query).Get("filenya");
                string filePath = _path + @"\" + fileName;
                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                using (var client = new HttpClientDownloadWithProgress(url, filePath))
                {
                    client.ProgressChanged += (totalFileSize, totalBytesDownloaded, progressPercentage) =>
                    {
                        //Console.WriteLine($"{progressPercentage}% ({totalBytesDownloaded}/{totalFileSize})");
                        if (progressPercentage != null)
                            Dispatcher.Invoke(() =>
                            {
                                pb.Value = (double) progressPercentage;
                                lb.Content = fileName + ": " + (totalBytesDownloaded/1000000.00).ToString("0.##") + "MB / " + ((long)totalFileSize/1000000.00).ToString("0.##") + "MB";

                            });
                        if (progressPercentage >= 100)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                pb.Value = 0;
                                if (pbid == 1)
                                {
                                    _pb1 = true;
                                }
                                if (pbid == 2)
                                {
                                    _pb2 = true;
                                }

                                lb.Content = "-";
                                pb_total.Value = _totalFile - listRemote.Count - 1;
                                lbl_total.Content = "Downloading: " + (_totalFile-listRemote.Count-1) + " / " + _totalFile;
                            });
                        }
                    };
                    await client.StartDownload();
                    downAct();
                }
            }
        }
        

        internal class Hasil
        {
            public string checksum { get; set; }
            public string alamat { get; set; }
        }
    }
}
