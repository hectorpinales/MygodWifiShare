﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using Mygod.Net;
using Mygod.Windows;
using ROOT.CIMV2.Win32;
using Action = System.Action;

namespace Mygod.Windows
{
    static class CurrentApp
    {
        private static AssemblyName NowAssemblyName { get { return Assembly.GetCallingAssembly().GetName(); } }
        public static Version Version { get { return NowAssemblyName.Version; } }
        public static string ProgramTitle { get { return NowAssemblyName.Name + @" V" + Version; } }
    }
}

namespace Mygod.WifiShare
{
    static class R
    {
        public static readonly string
            Requirement = @"{0} 需求配置：
 ※ 安装 .NET Framework 4.5.1 或更高（Windows 8.1 自带）
 ※ Windows 7 或更高版本的 Windows
 ※ 连接到 Internet
 ※ 有支持无线网络共享（按 A 查看）的 Wi-Fi 无线网络适配器",
            WelcomeToUse = @"欢迎使用 {0}！
可用操作：（输入其他退出）
    A 查看当前共享当前设置与状态          B 启动/重启共享    C 关闭共享
    D 深度重启共享                        K 刷新安全设置 (用后需要再次启动共享)
    I 初始化设置       S 杂项设置         T 设置开机自启动   L 查看日志
    Q 查看当前客户端   W 监视客户端       H 更多帮助         U 检查更新
请输入操作：",
            QuickHelp = @"一、第一次使用：输入 S 对无线网络名和密码进行设置，输入 B 启动无线网络共享，如果跳出网络选择，请选择家庭网络或工作网络，输入 I 并按照提示进行第一次配置。

二、再次使用：只需输入 B 启动无线网络共享即可。",
            Help = @"三、开机自动启动无线网络共享
0. 启动本软件。
1. 输入 T，如看到“当前设置自动启动无线网络共享。”的字样，关闭本程序，否则执行第2步。
2. 输入 Y 修改成不自动启动无线网络共享。

四、开机不自动启动无线网络共享
0. 启动本软件。
1. 输入 T，如看到“当前设置不自动启动无线网络共享。”的字样，关闭本程序，否则执行第2步。
2. 输入 Y 修改成自动启动无线网络共享。

五、可能碰到的问题：
①执行 一. 2 时出现“无法启动承载网络。组或资源的状态不是执行请求操作的正确状态。”字样：
　这可能是由于您的 Wi-Fi 没有打开，或者硬件开关被关闭导致，要解决只需打开即可。也有可能是无线网卡正在被使用导致的，这种情况下可以尝试以下步骤解决问题。
　　1. 点击右下角的“网络与共享中心”，如果当前连接到某无线网络，则选中当前连接到的无线网络，点击“断开”并执行第4步，否则执行第3步。
　　2. 打开“控制面板 - 网络和 Internet - 网络和共享中心 - 更改适配器设置”，禁用除了你当前的上网连接（如“本地连接”“宽带连接”）以外的所有连接并重新启用再试。
　　3. 再次执行 一. 2，若问题继续，请执行第3步。

②原来可以使用的网络共享时间长后无法继续使用：
　　1. 输入 B 重启共享。
　　2. 若问题持续，输入 D 深度重启共享。
    3. 若问题持续，输入 C 关闭后再次输入 D 深度重启。
    4. 若问题持续，重启电脑。

③Android 设备不断地显示“正在获取 IP 地址……”
    1. 按照②的方法重启共享。
    2. 若无效，在您的 Android 设备上按住你的热点名。
    3. 点击“修改网络”。
    4. 点击“显示高级选项”。
    5. 点击“IP 设置”，选“静态”。
    6. “IP 地址”中输入“192.168.137.xxx”，其中要将 xxx 替换成 2 至 255 中一个你喜欢的数（但愿你没有碰上冲突）。如果你在 一、7 中使用了别的静态 IP 地址，这里前缀也要对应修改。
    7. 在“域名 1”中输入“8.8.8.8”，“域名 2”中输入“8.8.4.4”。
    8. 点击“保存”，稍等片刻即可连上网络。若可以正常上网则说明设置没有问题，若不能请返回检查设置。

六、进阶使用：
此工具支持命令行参数，用法：
    Mygod无线网络共享.exe [命令序列]

若命令序列不为空，则程序运行完后将退出，不等待用户继续输入。

命令序列    相当于启动此工具后按的一系列键盘，支持除以下指令外的全部指令：H、I、S、T。";
    }

    static class Program
    {
        private const string RegistryPosition = @"HKEY_CURRENT_USER\Software\Mygod\ShareWifi\", RegistrySsid = "SSID",
                             RegistryKey = "Key", RegistryPeersCount = "PeersCount", RegistryTtl = "TTL",
                             TaskName = "MygodWifiShare";
        private static string ssid, key;
        private static int peersCount;
        internal static int Ttl;
        private static readonly DnsCache DnsCache = new DnsCache();

        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var exc = e.ExceptionObject as Exception;
                if (exc == null) return;
                Console.WriteLine("出现了未知错误！详情请查看临时目录下 MygodWifiShare.log 日志文件。");
                if (Logger.Initialized)
                    lock (Logger.Instance) Logger.Instance.Write("[{0}]\t{1}{2}",
                        DateTime.Now.ToString("yyyy.M.d H:mm:ss"), exc.GetMessage(), Environment.NewLine);
            };
            if (!Logger.Initialize()) return;
            Console.Title = CurrentApp.ProgramTitle;
            OutputRequirement();
            Console.WriteLine();
            if (CheckOS()) return;
            if (args.Length == 0)
                while (true)
                    if (SwitchOperation(ReadOperation(), false)) Console.WriteLine();
                    else return;
            UpdateSettings();
            foreach (var c in args.SelectMany(s => s)) SwitchOperation(c, true);
        }

        private static bool CheckOS()
        {
            if (Environment.OSVersion.Version.Major > 6
                || (Environment.OSVersion.Version.Major == 6 && Environment.OSVersion.Version.Minor >= 1)) return false;
            Console.Write("本程序必须在 Windows 7 或更高版本的 Windows 下运行！\n按任意键退出此程序。");
            Console.ReadKey();
            return true;
        }

        /// <summary>
        /// 用于将错误转化为可读的字符串。
        /// </summary>
        /// <param name="e">错误。</param>
        /// <returns>错误字符串。</returns>
        private static string GetMessage(this Exception e)
        {
            var result = new StringBuilder();
            GetMessage(e, result);
            return result.ToString();
        }
        private static void GetMessage(Exception e, StringBuilder result)
        {
            while (e != null && !(e is AggregateException))
            {
                result.AppendFormat("({0}) {1}\n{2}\n", e.GetType(), e.Message, e.StackTrace);
                e = e.InnerException;
            }
            var ae = e as AggregateException;
            if (ae != null) foreach (var ex in ae.InnerExceptions) GetMessage(ex, result);
        }
        private static void Try(Action tryAction, Action failAction = null)
        {
            try
            {
                if (tryAction != null) tryAction();
            }
            catch (Exception exc)
            {
                Console.WriteLine("未知错误：" + exc.GetMessage());
                if (failAction != null) failAction();
            }
        }
        private static void WriteReason(WlanHostedNetworkReason reason)
        {
            Console.WriteLine(WlanManager.ToString(reason));
        }

        private static void SetAutoRun()
        {
            using (var service = new TaskService())
            {
                var task = service.FindTask(TaskName);
                Console.WriteLine("当前设置{0}自动启动无线网络共享。", task == null ? "不" : string.Empty);
                Console.Write("修改吗？(Y 确定，其他取消)");
                if (char.ToUpper(Console.ReadKey().KeyChar) != 'Y') return;
                Console.WriteLine();
                if (task == null)
                {
                    var def = service.NewTask();
                    def.Triggers.Add(new LogonTrigger { UserId = WindowsIdentity.GetCurrent().Name });
                    def.Actions.Add(new ExecAction(Assembly.GetEntryAssembly().Location, "B"));
                    def.Principal.RunLevel = TaskRunLevel.Highest;  // MOST IMPORTANT FIX
                    service.RootFolder.RegisterTaskDefinition(TaskName, def);
                }
                else service.RootFolder.DeleteTask(TaskName);
            }
            Console.WriteLine("修改自动运行完成。");
        }
        private static void RestartInternetConnectionSharingService()
        {
            try
            {
                var service = new ServiceController("SharedAccess");
                Console.WriteLine("正在停止服务 {0}……", service.DisplayName);
                service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 1, 0));
                Console.WriteLine("正在启动服务 {0}……", service.DisplayName);
                service.Start();
                service.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 1, 0));
            }
            catch (Exception exc)
            {
                Console.WriteLine("重启服务时出现错误：{0}", exc.Message);
            }
        }
        private static void Close()
        {
            Console.Write("正在关闭共享……");
            WriteReason(WlanManager.ForceStop());
        }
        private static void Boot()
        {
            Close();
            Try(() =>
            {
                WlanHostedNetworkReason reason;
                Console.Write("启用托管网络中……");
                WriteReason(reason = WlanManager.SetEnabled(true));
                if (reason != WlanHostedNetworkReason.Success)
                {
                    Close();
                    return;
                }
                Console.Write("正在应用设置……");
                reason = WlanManager.SetConnectionSettings(ssid, peersCount);
                if (reason != WlanHostedNetworkReason.Success)
                {
                    WriteReason(reason);
                    Close();
                    return;
                }
                WriteReason(reason = WlanManager.SetSecondaryKey(key));
                if (reason != WlanHostedNetworkReason.Success)
                {
                    Close();
                    return;
                }
                Console.Write("启动共享中……");
                WriteReason(WlanManager.ForceStart());
            }, Close);
        }
        private static void Init()
        {
            Console.WriteLine("配置 Microsoft 托管网络虚拟适配器中……");
            var virtualAdapter = new ManagementObjectSearcher(
                    new SelectQuery("Win32_NetworkAdapter", "PhysicalAdapter=1 AND ServiceName='vwifimp'"))
                .Get().OfType<ManagementObject>().Select(result => new NetworkAdapter(result)).SingleOrDefault();
            if (virtualAdapter == null)
            {
                Console.WriteLine("查询 Microsoft 托管网络虚拟适配器失败！请先启动无线网络共享后再试。");
                return;
            }
            virtualAdapter.NetConnectionID = "无线网络共享";
            var mo = new ManagementObjectSearcher(new SelectQuery("Win32_NetworkAdapterConfiguration",
                                                  string.Format("SettingID='{0}'", virtualAdapter.GUID)))
                            .Get().OfType<ManagementObject>().SingleOrDefault();
            if (mo == null)
            {
                Console.WriteLine("查询 Microsoft 托管网络虚拟适配器具体配置失败！请先启动无线网络共享后再试。");
                return;
            }
            mo.InvokeMethod("EnableStatic", new object[] { new[] { "192.168.137.1" }, new[] { "255.255.255.0" } });
            mo.InvokeMethod("SetGateways", new object[] { new string[0], new ushort[0] });
            mo.InvokeMethod("SetDNSServerSearchOrder", new object[] { new[] { "8.8.8.8", "8.8.4.4" } });
            Console.WriteLine("搜索可用网络连接中……");
            dynamic manager = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid
                ("5C63C1AD-3956-4FF8-8486-40034758315B")));
            dynamic virtualConnection = null;
            var query = new List<Tuple<dynamic, dynamic>>();
            foreach (var connection in manager.EnumEveryConnection)
            {
                var props = manager.NetConnectionProps[connection];
                if (props.Guid == virtualAdapter.GUID)
                    if (virtualConnection == null) virtualConnection = connection;
                    else return;
                else if (props.Status == 2) // NCS_CONNECTED
                {
                    Console.WriteLine("{0}. {1} ({2})", query.Count, props.Name, props.DeviceName);
                    query.Add(new Tuple<dynamic, dynamic>(connection, props.Guid));
                }
            }
            if (query.Count > 0)
            {
                var picked = 0;
                if (query.Count > 1)
                {
                    Console.Write("请选择要共享的网络连接序号：（即前面的序号，输个错误的取消操作）");
                    if (!int.TryParse(Console.ReadLine(), out picked) || picked < 0 || picked >= query.Count) return;
                }
                else Console.WriteLine("共享唯一可用的网络连接中……");
                foreach (var connection in manager.EnumEveryConnection)
                {
                    var conf = manager.INetSharingConfigurationForINetConnection[connection];
                    var props = manager.NetConnectionProps[connection];
                    if (conf.SharingEnabled && (props.Guid != query[picked].Item2 || conf.SharingConnectionType != 0)
                        && (props.Guid != virtualAdapter.GUID && conf.SharingConnectionType != 1))
                        conf.DisableSharing();
                }
                var tempConf = manager.INetSharingConfigurationForINetConnection[query[picked].Item1];
                if (!tempConf.SharingEnabled) tempConf.EnableSharing(0);    // ICSSHARINGTYPE_PRIVATE
                tempConf = manager.INetSharingConfigurationForINetConnection[virtualConnection];
                if (!tempConf.SharingEnabled) tempConf.EnableSharing(1);    // ICSSHARINGTYPE_PUBLIC
                Console.WriteLine("初始化设置完成！");
            }
            else Console.WriteLine("没有可用的网络连接！");
        }
        private static void RefreshKey()
        {
            Console.Write("正在刷新安全设置……");
            Try(() => WriteReason(WlanManager.RefreshSecuritySettings()));
        }
        private static void Settings()
        {
            Console.WriteLine("请输入您的新的设置，为空则不修改。");
            Console.WriteLine("旧的无线网络名：" + ssid);
            Console.Write("新的无线网络名：");
            ssid = Console.ReadLine();
            Console.WriteLine("旧的无线密码：" + key);
            Console.Write("新的无线密码：");
            key = Console.ReadLine();
            var changed = false;
            if (!string.IsNullOrEmpty(ssid)) 
            { 
                Registry.SetValue(RegistryPosition, RegistrySsid, ssid);
                changed = true;
            }
            if (key != null && key.Length >= 8 && key.Length < 64)
            {
                Registry.SetValue(RegistryPosition, RegistryKey, key);
                changed = true;
            }
            Console.WriteLine("旧的最大客户端数：" + peersCount);
            Console.Write("新的最大客户端数：");
            if (int.TryParse(Console.ReadLine(), out peersCount) && peersCount >= 0)
            {
                Registry.SetValue(RegistryPosition, RegistryPeersCount, peersCount);
                changed = true;
            }
            Console.WriteLine("TTL：DNS 查询缓存时间，0 表示不缓存，若不明觉厉请最好不要修改。（单位：秒）");
            Console.WriteLine("旧的 TTL：" + Ttl);
            Console.Write("新的 TTL：");
            if (int.TryParse(Console.ReadLine(), out Ttl) && Ttl >= 0)
                Registry.SetValue(RegistryPosition, RegistryTtl, Ttl);
            if (!changed) return;
            Console.Write("修改完毕，是否要立即生效？(Y 生效，其他键不生效)");
            var ch = char.ToUpper(Console.ReadKey().KeyChar);
            Console.WriteLine();
            if (ch != 'Y') return;
            UpdateSettings();
            Boot();
        }
        private static void CheckForUpdates()
        {
            WebsiteManager.CheckForUpdates(() => Console.WriteLine("没有可用更新。"),
                                           exc => Console.WriteLine("检查更新失败。\n错误信息：" + exc.GetMessage()));
        }
        private static void ShowHelp()
        {
            Console.WriteLine(R.QuickHelp);
            Console.WriteLine();
            Console.WriteLine(R.Help);
        }
        
        private static void ShowStatus()
        {
            Try(() =>
            {
                WlanOpcodeValueType opCode;
                try
                {
                    WlanHostedNetworkConnectionSettings cs;
                    opCode = WlanManager.QueryConnectionSettings(out cs);
                    Console.WriteLine("托管网络连接设置来源：\t{0}\nSSID：\t\t\t{1}\n最大客户端数：\t\t{2}",
                                      WlanManager.ToString(opCode), cs.HostedNetworkSSID.Content, cs.MaxNumberOfPeers);
                }
                catch (BadConfigurationException)
                {
                }
                WlanHostedNetworkSecuritySettings ss;
                opCode = WlanManager.QuerySecuritySettings(out ss);
                Console.WriteLine("托管网络安全设置来源：\t{0}\n验证算法：\t\t{1}\n加密算法：\t\t{2}",
                                  WlanManager.ToString(opCode),
                                  WlanManager.ToString(ss.Dot11AuthAlgo), WlanManager.ToString(ss.Dot11CipherAlgo));
                try
                {
                    string profile;
                    opCode = WlanManager.QueryStationProfile(out profile);
                    Console.Write("托管网络配置文件来源：\t{0}\n配置文件：\n{1}",
                                  WlanManager.ToString(opCode), profile.Trim('\0'));
                }
                catch (BadConfigurationException)
                {
                }
                bool enabled;
                opCode = WlanManager.QueryEnabled(out enabled);
                Console.WriteLine("托管网络启用设置来源：\t{0}\n托管网络启用：\t\t{1}",
                                  WlanManager.ToString(opCode), enabled ? '是' : '否');
                string passKey;
                bool isPassPhrase, isPersistent;
                var reason = WlanManager.QuerySecondaryKey(out passKey, out isPassPhrase, out isPersistent);
                if (reason == WlanHostedNetworkReason.Success)
                    Console.WriteLine("密码：\t\t\t{0}\n使用密码短语：\t\t{1}\n永久密码：\t\t{2}",
                                      passKey, isPassPhrase ? '是' : '否', isPersistent ? '是' : '否');
                else Console.WriteLine("查询密码失败，原因：" + WlanManager.ToString(reason));
                var status = WlanManager.QueryStatus();
                Console.WriteLine("状态：\t\t\t" + WlanManager.ToString(status.HostedNetworkState));
                if (status.HostedNetworkState == WlanHostedNetworkState.Unavailable) return;
                Console.WriteLine("实际网络 ID：\t\t{0}\nBSSID：\t\t\t{1}",
                                  status.IPDeviceID, status.wlanHostedNetworkBSSID);
                if (status.HostedNetworkState == WlanHostedNetworkState.Active)
                    Console.WriteLine("802.11 物理层类型：\t{0}\n网络接口信道频率：\t{1}\n已认证客户端数量：\t{2}",
                                      WlanManager.ToString(status.dot11PhyType), status.ulChannelFrequency,
                                      status.dwNumberOfPeers);
            });
        }
        public static ILookup<string, Arp.MibIpNetRow> Lookup
            { get { return Arp.GetIpNetTable().ToLookup(row => row.MacAddress, row => row); }}

        public static string GetDeviceDetails(WlanHostedNetworkPeerState peer, bool wait = false,
                                              ILookup<string, Arp.MibIpNetRow> lookup = null, string padding = "")
        {
            var result = string.Format("物理地址：{0} ({1})\n", peer.PeerMacAddress,
                                       WlanManager.ToString(peer.PeerAuthState));
            var ips = (lookup ?? Lookup)[peer.PeerMacAddress.ToString()].ToArray();
            if (ips.Length > 0) result += string.Format("{0}IP  地址：{1}\n", padding,
                string.Join("\n          " + padding, ips.Select(ip =>
                {
                    var domains = DnsCache.GetDomains(ip.IPAddress, wait || Ttl == 0);
                    return ip.ToString() + (domains == null ? string.Empty : (" [" + domains + ']'));
                })));
            return result;
        }
        private static string QueryCurrentDevices(bool wait = false)
        {
            try
            {
                var status = WlanManager.QueryStatus();
                if (status.HostedNetworkState != WlanHostedNetworkState.Active)
                    return "查询客户端失败，托管网络尚未启用。";
                var result = new StringBuilder();
                var lookup = Lookup;
                var i = 0;
                foreach (var peer in status.PeerList)
                {
                    i++;
                    result.AppendFormat("设备 #{0} {1}", i, GetDeviceDetails(peer, wait, lookup,
                        string.Empty.PadLeft(8 + (int) Math.Floor(Math.Log10(i)))));
                }
                return result.ToString();
            }
            catch (Exception exc)
            {
                return "查询客户端失败，详细信息：" + exc.GetMessage();
            }
        }
        private static void WatchCurrentDevices()
        {
            while (true)
            {
                if (Console.KeyAvailable && Console.ReadKey().Key == ConsoleKey.Escape) break;
                var result = QueryCurrentDevices();
                Console.Clear();
                Console.WriteLine("监视已连接设备中，按 Esc 键返回。");
                Console.WriteLine(result);      // prevent flashing
                Thread.Sleep(500);
            }
            Console.Clear();
        }

        private static void OutputLog()
        {
            Process.Start(Logger.LogPath);
        }
        
        private static void OutputRequirement()
        {
            Console.WriteLine(R.Requirement, CurrentApp.ProgramTitle);
            Console.WriteLine();
            Console.WriteLine(R.QuickHelp);
        }
        private static void UpdateSettings()
        {
            try
            {
                ssid = (string)Registry.GetValue(RegistryPosition, RegistrySsid, null);
            }
            catch (FormatException)
            {
                ssid = null;
            }
            if (ssid == null) Registry.SetValue(RegistryPosition, RegistrySsid, ssid = "Mygod Hotspot");
            try
            {
                key = (string)Registry.GetValue(RegistryPosition, RegistryKey, null);
            }
            catch (FormatException)
            {
                key = null;
            }
            if (key == null) Registry.SetValue(RegistryPosition, RegistryKey, key = "AwesomePassword");
            try
            {
                peersCount = (int)Registry.GetValue(RegistryPosition, RegistryPeersCount, null);
            }
            catch
            {
                peersCount = -1;
            }
            if (peersCount < 0) Registry.SetValue(RegistryPosition, RegistryPeersCount, peersCount = 100);
            try
            {
                Ttl = (int)Registry.GetValue(RegistryPosition, RegistryTtl, null);
            }
            catch
            {
                Ttl = -1;
            }
            if (Ttl < 0) Registry.SetValue(RegistryPosition, RegistryTtl, Ttl = 300);
        }
        private static char ReadOperation()
        {
            UpdateSettings();
            Console.Write(R.WelcomeToUse, CurrentApp.ProgramTitle);
            var result = char.ToUpper(Console.ReadKey().KeyChar);
            Console.WriteLine();
            return result;
        }
        private static bool SwitchOperation(char operation, bool auto)
        {
            switch (char.ToUpper(operation))
            {
                case 'A':
                    ShowStatus();
                    break;
                case 'B':
                    Boot();
                    break;
                case 'C':
                    Close();
                    break;
                case 'D':
                    RestartInternetConnectionSharingService();
                    Boot();
                    break;
                case 'H':
                    if (auto) Console.WriteLine("自动状态下拒绝显示帮助！");
                    else ShowHelp();
                    break;
                case 'I':
                    if (auto) Console.WriteLine("自动状态下拒绝初始化设置！");
                    else Init();
                    break;
                case 'K':
                    RefreshKey();
                    break;
                case 'L':
                    OutputLog();
                    break;
                case 'Q':
                    Console.WriteLine(QueryCurrentDevices(true));
                    break;
                case 'S':
                    if (auto) Console.WriteLine("自动状态下拒绝设置！");
                    else Settings();
                    break;
                case 'T':
                    if (auto) Console.WriteLine("自动状态下拒绝设置自动启动！");
                    else SetAutoRun();
                    break;
                case 'U':
                    CheckForUpdates();
                    break;
                case 'W':
                    WatchCurrentDevices();
                    break;
                default:
                    return false;
            }
            Console.WriteLine();
            return true;
        }
    }
}
