using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.IoC;
using Dalamud.Plugin;
using System;
using System.Threading;
using System.IO;
using Newtonsoft.Json.Linq;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

#pragma warning disable CA1416
namespace CatchingWay
{
    public class CatchingWay : IDalamudPlugin, IDisposable
    {
        [PluginService] public static ChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ClientState ClientState { get; private set; } = null!;

        internal static uint[] crcTable;
        internal static string CopyRightText = "SQUARE ENIX CO.,LTD. All Rights Reserved. FINAL FANTASY XIV";
        internal static string MYDOCUMENT = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public string Name => "CatchingWay";

        internal string ffxiv_cfg = "My Games\\FINAL FANTASY XIV - A Realm Reborn\\FFXIV.cfg";
        internal List<FileSystemWatcher> WatcherList;

        public CatchingWay()
        {
            if (File.Exists(Path.Combine(Environment.CurrentDirectory, "pid.txt")))
            {
                string pid = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, "pid.txt"));
                if (Process.GetProcessById(int.Parse(pid)) != null)
                {
                    ChatGui.PrintChat(new() { Message = "Automatically Disabled", Type = Dalamud.Game.Text.XivChatType.SystemMessage });
                }
            }
            else
            {
                Init();
            }
        }

        private void Init()
        {
            WatcherList = new();
            ManagingWay(Path.Combine(MYDOCUMENT, ffxiv_cfg), '\t', "ScreenShotDir");
            ManagingWay(Path.Combine(Environment.CurrentDirectory, "ReShade.ini"), '=', "SavePath");

            File.WriteAllText("pid.txt", Environment.ProcessId.ToString());
        }

        private void ManagingWay(string cfgpath, char sep, string key)
        {
            FileSystemWatcher Watcher;
            string keydir = "";
            if (File.Exists(cfgpath))
            {
                string[] lines = File.ReadAllLines(cfgpath);
                foreach (var i in lines)
                {
                    if (i.Split(sep)[0] == key)
                    {
                        keydir = i.Split(sep)[1].Trim();
                    }
                }
            }
            if (Directory.Exists(keydir))
            {
                Watcher = new(keydir)
                {
                    Filter = "",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                };
                Watcher.Created += WatchingWay;
                Watcher.EnableRaisingEvents = true;
                WatcherList.Add(Watcher);
            }
        }

        private void WatchingWay(object sender, FileSystemEventArgs e)
        {
            JObject info = GettingWay();
            new Thread(new ThreadStart(() =>
            {
                Thread.Sleep(2500);
                try
                {
                    if (e.FullPath.EndsWith(".png"))
                    {
                        byte[] png = File.ReadAllBytes(e.FullPath);
                        int header_position = 0;
                        using (MemoryStream ms = new(png))
                        {
                            for(;ms.Position + 8 <= ms.Length;)
                            {
                                ms.Seek(8, SeekOrigin.Begin);
                                byte[] length = new byte[4];
                                byte[] tagname = new byte[4];
                                byte[] crc = new byte[4];
                                ms.Read(length, 0, 4);
                                Array.Reverse(length);
                                ms.Read(tagname, 0, 4);
                                int datalen = BitConverter.ToInt32(length);
                                if (datalen > 0)
                                {
                                    ms.Seek(datalen, SeekOrigin.Current);
                                }
                                ms.Read(crc, 0, 4);
                                if (Encoding.UTF8.GetString(tagname) == "IHDR")
                                {
                                    header_position = (int)ms.Position;
                                    break;
                                }
                            }
                        }

                        if (header_position != 0)
                        {
                            using MemoryStream stream = new();
                            stream.Write(png.Take(header_position).ToArray(), 0, header_position);
                            stream.Seek(header_position, SeekOrigin.Begin);

                            byte[] author = CreateItxtChunk("Author", ClientState.LocalPlayer.Name.TextValue);
                            byte[] copyright = CreateItxtChunk("Copyright", $"ⓒ 2010-{DateTime.Now:yyyy} {CopyRightText}");
                            byte[] date = CreateTextChunk("Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                            byte[] software = CreateTextChunk("Software", "Final Fantasy XIV");
                            byte[] posx = CreateTextChunk("PositionX", info["x"].ToString());
                            byte[] posy = CreateTextChunk("PositionY", info["y"].ToString());
                            byte[] posz = CreateTextChunk("PositionZ", info["z"].ToString());
                            byte[] heading = CreateTextChunk("Heading", info["heading"].ToString());
                            byte[] jobid = CreateTextChunk("JobID", info["job"].ToString());
                            byte[] zoneid = CreateTextChunk("ZoneID", info["zone"].ToString());

                            stream.Write(author, 0, author.Length);
                            stream.Write(copyright, 0, copyright.Length);
                            stream.Write(date, 0, date.Length);
                            stream.Write(software, 0, software.Length);
                            stream.Write(posx, 0, posx.Length);
                            stream.Write(posy, 0, posy.Length);
                            stream.Write(posz, 0, posz.Length);
                            stream.Write(heading, 0, heading.Length);
                            stream.Write(zoneid, 0, zoneid.Length);
                            stream.Write(jobid, 0, jobid.Length);
                            byte[] newpng = png.Skip(header_position).ToArray();
                            stream.Write(newpng, 0, newpng.Length);

                            using FileStream fs = new(e.FullPath, FileMode.Open);
                            stream.WriteTo(fs);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ChatGui.Print(ex.Message);
                }
            })).Start();
        }

        public void Dispose()
        {
            try
            {
                if (WatcherList != null)
                {
                    File.Delete(Path.Combine(Environment.CurrentDirectory, "pid.txt"));
                    for (var i = 0; i < WatcherList.Count; i++)
                    {
                        WatcherList[i].Dispose();
                    }
                }
            }
            catch { }
            finally
            {
                GC.SuppressFinalize(this);
            }
        }

        private static JObject GettingWay()
        {
            try
            {
                JObject obj = new()
                {
                    ["job"] = ClientState.LocalPlayer.ClassJob.Id,
                    ["zone"] = ClientState.TerritoryType,
                    ["x"] = ClientState.LocalPlayer.Position.X,
                    ["y"] = ClientState.LocalPlayer.Position.Y,
                    ["z"] = ClientState.LocalPlayer.Position.Z,
                    ["heading"] = ClientState.LocalPlayer.Rotation
                };
                return obj;
            }
            catch
            {
                return new JObject()
                {
                    ["job"] = 0,
                    ["zone"] = 0,
                    ["x"] = 0,
                    ["y"] = 0,
                    ["z"] = 0,
                    ["heading"] = 0
                };
            }
        }

        private static byte[] ChunkingWay(string data)
        {
            byte[] itxt = Encoding.UTF8.GetBytes(data);
            byte[] itxt_len = BitConverter.GetBytes(itxt.Length - 4);
            uint CRC = CheckingWay(itxt, 0, itxt.Length, 0);
            Array.Reverse(itxt_len);
            return itxt_len.Concat(itxt).Concat(BitConverter.GetBytes(CRC)).ToArray();
        }

        private static byte[] CreateItxtChunk(string keyword, string data)
        {
            return ChunkingWay($"iTXt{keyword}\0\0\0\0\0{data}");
        }

        private static byte[] CreateTextChunk(string keyword, string data)
        {
            return ChunkingWay($"tEXt{keyword}\0{data}");
        }

        private static uint CheckingWay(byte[] data, int offset, int length, int crc)
        {
            uint c;
            if (crcTable == null)
            {
                crcTable = new uint[256];
                for (uint n = 0; n <= 255; n++)
                {
                    c = n;
                    for (var k = 0; k <= 7; k++)
                    {
                        if ((c & 1) == 1)
                            c = 0xEDB88320 ^ ((c >> 1) & 0x7FFFFFFF);
                        else
                            c = (c >> 1) & 0x7FFFFFFF;

                        crcTable[n] = c;
                    }
                }
            }
            c = (uint)(crc ^ 0xFFFFFFFF);
            var eof = offset + length;
            for (var i = offset; i < eof; i++)
            {
                c = crcTable[(c ^ data[i]) & 255] ^ ((c >> 8) & 0xFFFFFF);
            }
            return c ^ 0xFFFFFFFF;
        }
    }
}
#pragma warning restore CA1416
