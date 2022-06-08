using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
using System.IO;

namespace AddWiFiTest
{
    public partial class Form1 : Form
    {
        private struct AP_Config
        {
            public string name;
            public string pass;
        }

        private static List<AP_Config> myAPList = new List<AP_Config>();

        // 16 chars
        private const string AES_IV_256 = @"ZCp46URTuOMFlsJ3";
        // 32 chars
        private const string AES_Key_256 = @"Km28vndgjGVn0p4NPLk0zAFGrTzImC0e";

        // window
        private const int WINDOW = 8;
        private const int START_POS = 6;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if ((WINDOW <= 0) || (StartPosition <= 0) || (AES_IV_256.Length != 16) || (AES_Key_256.Length != 32))
            {
                MessageBox.Show("内部設定が異常です");
                this.Close();
            }

            // check execute drive (NTFS only)
            string currentDrive = System.IO.Path.GetPathRoot(Application.ExecutablePath).ToLower();
            DriveInfo[] infos = DriveInfo.GetDrives();
            bool find = false;
            foreach(DriveInfo info in infos)
            {
                if (info.Name.ToLower() == currentDrive)
                {
                    if (info.DriveFormat == "NTFS")
                    {
                        find = true;
                        break;
                    }
                    else
                    {
                        break;
                    }
                }
            }
            if (!find)
            {
                MessageBox.Show("このファイルをデスクトップにコピーしてから実行してください");
                this.Close();
            }

            // set your APs here
            myAPList.Add(
                new AP_Config{
                    name = "AP-1-A",
                    pass = "wg3e159XxNBbYB3E61QWDLYpSFdeeSTcdqdf1I3rLIEwLjaXqZ8zriFXyQAQHJywHFKVeqKxUvUkQc8icjxza6qk5/oViVIHw5P3c+SXSClD2wZXa6xOC+W7XPVsVVRo"
                }
            );
            //myAPList.Add(
            //    new AP_Config
            //    {
            //        name = "My-Good-AP2",
            //        pass = "Dzwaaw8wcGvnwduwOMcnx45i9jn7uETG12WsvIUA+YIVCwWeQrsd2vKhvsXPOkO5nG7D8cGNTqhr0nd4yU4ojpQHpvWO82XnMDYEPZ0ObcVToV10mI4eZxO8eOiCpzfP"
            //    }
            //);
            //myAPList.Add(
            //    new AP_Config
            //    {
            //        name = "My-Good-AP3",
            //        pass = "x7HNnqCPK/XR9I9tlb6W03mLuJgYsiAL80fMbSxkaJ+lJTCtseYROGo3JLAZDiXbKSCwF0YERbwXVJ7nHi7B/d7RpcBJ20vULVmL/eo6I9JRoV6ZOJ6cAsP51cf8PSYf"
            //    }
            //);

            string[] cmd = System.Environment.GetCommandLineArgs();
            if ((cmd.Length >= 2) && (cmd[1].ToLower() == "/s"))
            {
                txtPass.Visible = true;
                btnPass.Visible = true;
            }
            else
            {
                btnPass.Visible = false;
                txtPass.Visible = false;
            }
        }

        private void btnSet_Click(object sender, EventArgs e)
        {
            btnSet.Enabled = false;
            overwriteAPs();
            btnSet.Enabled = true;
        }

        private static void overwriteAPs()
        {
            List<string> aps = getAPList();

            string currentPath = System.IO.Path.GetDirectoryName(Application.ExecutablePath);

            System.Diagnostics.Process p = new System.Diagnostics.Process();

            p.StartInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = false;

            p.StartInfo.CreateNoWindow = true;

            foreach (string ap in aps)
            {
                foreach (AP_Config t in myAPList)
                {
                    if (ap == t.name)
                    {
                        // export
                        p.StartInfo.Arguments = "/c netsh wlan export profile name=\"" + ap + "\" folder=\"" + currentPath + "\"";
                        p.Start();
                        p.WaitForExit();

                        bool isExported = false;
                        System.Threading.Thread.Sleep(500);
                        string xmlPath = System.IO.Path.Combine(currentPath, "Wi-Fi-" + ap + ".xml");
                        for (int i = 0; i < 5; i++)
                        {
                            if (System.IO.File.Exists(xmlPath))
                            {
                                isExported = true;
                                break;
                            }
                            System.Threading.Thread.Sleep(500);
                        }
                        if (!isExported)
                        {
                            MessageBox.Show("設定に失敗しましたので、中断します\n" + xmlPath);
                            break;
                        }

                        // remove
                        p.StartInfo.Arguments = "/c netsh wlan delete profile name=\"" + ap + "\"";
                        p.Start();
                        p.WaitForExit();

                        // add
                        p.StartInfo.Arguments = "/c netsh wlan add profile file=\"" + xmlPath + "\"";
                        p.Start();
                        p.WaitForExit();

                        // password
                        p.StartInfo.Arguments = "/c netsh wlan set profileparameter name=\"" + ap + "\" keyMaterial=" + Decrypt(t.pass);
                        p.Start();
                        p.WaitForExit();

                        // remove file
                        p.StartInfo.Arguments = "/c del /Q " + xmlPath;
                        p.Start();
                        p.WaitForExit();

                        // DBG
                        //Console.WriteLine("AP: " + ap);
                        //Console.WriteLine("Pass: " + Decrypt(t.pass));

                        break;
                    }
                }
            }

            p.Close();
        }

        private void btnPassword_Click(object sender, EventArgs e)
        {
            string text = txtPass.Text;

            string result = "plane: " + text + "\n";

            string enc = Encrypt(text);
            string dec = Decrypt(enc);

            result += "enc: " + enc + "\n" + "dec: " + dec + "\n";
            result += "Copy to Clipboard";

            MessageBox.Show(result);
            Clipboard.SetText(enc);
        }

        private static string Encrypt(string text)
        {
            using (var myRijndael = new RijndaelManaged())
            {
                myRijndael.BlockSize = 128;
                myRijndael.KeySize = 256;

                myRijndael.Mode = CipherMode.CBC;
                myRijndael.Padding = PaddingMode.PKCS7;

                myRijndael.IV = Encoding.UTF8.GetBytes(AES_IV_256);
                myRijndael.Key = Encoding.UTF8.GetBytes(AES_Key_256);

                ICryptoTransform encryptor = myRijndael.CreateEncryptor(myRijndael.Key, myRijndael.IV);

                Random rand = new Random();
                int len = text.Length;
                int bef = START_POS % WINDOW;
                string newText = "";
                for (int i = 0; i < len; i++)
                {
                    for (int j = 0; j < bef; j++)
                    {
                        newText += Convert.ToChar(rand.Next(33, 127));
                    }
                    newText += text.Substring(i, 1);
                    for (int j = bef + 1; j < WINDOW; j++)
                    {
                        newText += Convert.ToChar(rand.Next(33, 127));
                    }
                    bef = (++bef) % WINDOW;
                }

                byte[] encrypted;
                using (MemoryStream mStream = new MemoryStream())
                {
                    using (CryptoStream ctStream = new CryptoStream(mStream, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter sw = new StreamWriter(ctStream))
                        {
                            sw.Write(newText);
                        }
                        encrypted = mStream.ToArray();
                    }
                }

                return (System.Convert.ToBase64String(encrypted));
            }
        }

        private static string Decrypt(string text)
        {
            using (var myRijndael = new RijndaelManaged())
            {
                myRijndael.BlockSize = 128;
                myRijndael.KeySize = 256;
                
                myRijndael.Mode = CipherMode.CBC;
                myRijndael.Padding = PaddingMode.PKCS7;

                myRijndael.IV = Encoding.UTF8.GetBytes(AES_IV_256);
                myRijndael.Key = Encoding.UTF8.GetBytes(AES_Key_256);

                ICryptoTransform decryptor = myRijndael.CreateDecryptor(myRijndael.Key, myRijndael.IV);

                string plain = string.Empty;
                using (MemoryStream mStream = new MemoryStream(System.Convert.FromBase64String(text)))
                {
                    using (CryptoStream ctStream = new CryptoStream(mStream, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader sr = new StreamReader(ctStream))
                        {
                            plain = sr.ReadLine();
                        }
                    }
                }

                string newPlain = "";

                int pos = START_POS % WINDOW;
                while (plain != "")
                {
                    string t = plain.Substring(0, WINDOW);
                    newPlain += t.Substring(pos, 1);
                    pos = (++pos) % WINDOW;
                    plain = plain.Substring(WINDOW);
                }

                return newPlain;
            }
        }

        private static List<string> getAPList()
        {
            System.Diagnostics.Process p = new System.Diagnostics.Process();

            p.StartInfo.FileName = System.Environment.GetEnvironmentVariable("ComSpec");

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = false;

            p.StartInfo.CreateNoWindow = true;

            p.StartInfo.Arguments = "/c netsh wlan show profile";

            p.Start();

            string result = p.StandardOutput.ReadToEnd();

            p.WaitForExit();
            p.Close();

            // DBG
            //MessageBox.Show(result);

            string[] spt = result.Split('\n');
            //MessageBox.Show(spt.Length.ToString());

            List<string> aps = new List<string>();

            foreach (string trg in spt)
            {
                if (trg.Contains(":"))
                {
                    string[] ts = trg.Trim().Split(':');
                    if (ts.Length == 2)
                    {
                        if (ts[0].Trim() == "すべてのユーザー プロファイル")
                        {
                            aps.Add(ts[1].Trim());
                        }
                    }
                }
            }

            return aps;

            //string ap_list = "";
            //foreach(string a in aps)
            //{
            //    ap_list += a + "\n";
            //}
            //MessageBox.Show(ap_list);
        }
    }
}
