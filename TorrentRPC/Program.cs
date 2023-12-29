using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Transmission.API.RPC;
using Transmission.API.RPC.Entity;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;
using IniParser;
using IniParser.Model;

namespace TorrentRPC
{
    class Program
    {

        // some defaults but use ini as refence
        static string watchDir = "spool";
        static string destDir = "hoarded";
        static string invalidDir = "invalid";

        static string log = "hoarded.log";

        static string url = "http://192.168.0.1:9091/transmission/rpc";
        static string username = "transmission";
        static string password = "transmission";

        static string[] extensions = new string[] { };

        static int sleep = 60000;

        static void Main(string[] args)
        {

            var parser = new FileIniDataParser();

            string configName = "config.ini";

            if (args != null && args.Length >= 1)
            {
                if (!String.IsNullOrEmpty(args[0]))
                    configName = args[0];
            }

            try
            {
                IniData data = parser.ReadFile(configName);
                watchDir = data["Main"]["watchDir"];
                destDir = data["Main"]["destDir"];
                invalidDir = data["Main"]["invalidDir"];
                log = data["Main"]["log"];

                url = data["Transmission"]["url"];
                username = data["Transmission"]["username"];
                password = data["Transmission"]["password"];

                sleep = int.Parse(data["Main"]["sleep"]);
                extensions = data["Main"]["extensions"].Split(',');
            }
            catch { }
           
            while (true)
            {
                try
                {
                    var files = Directory.EnumerateFiles(watchDir);

                    foreach (var file in files)
                    {
                        if (!file.ToLower().EndsWith(".torrent"))
                        {
                            invalidTorrentFile(file);
                            continue;
                        }

                        if (!isTorrentHoardable(file))
                        {
                            invalidTorrentFile(file);
                            continue;
                        }

                        try
                        {
                            string hash = uploadTorrentFile(file);
                            finalizeTorrentFile(file);
                            File.AppendAllLines(log, new string[] { hash });
                        }
                        catch
                        {

                        }
                    }
                }
                catch { }
                Thread.Sleep(sleep);
            }

        }

        static bool isExtensionHoardable(string filename)
        {
            string file = filename.ToLower();

            if (extensions.Length == 0)
                return true; // feature disabled accepting any

            foreach (string ext in extensions)
            {
                if (file.EndsWith(ext.ToLower()))
                    return true;
            }

            return false;
        }
        static bool isTorrentHoardable(string path)
        {
            // Parse torrent by specifying the file path
            var parser = new BencodeParser(); // Default encoding is Encoding.UTF8, but you can specify another if you need to
            Torrent torrent = parser.Parse<Torrent>(path);
            //BDictionary bdictinoary = torrent.ToBDictionary();

            if (torrent.Files == null)
            {
                return isExtensionHoardable(torrent.File.FileName);
            } else
            {
                foreach (var file in torrent.Files)
                {
                    try
                    {
                        if (isExtensionHoardable(file.FileName))
                        {
                            return true;
                        }
                    }
                    catch { }
                }
            }

            return false;

        }

        static void finalizeTorrentFile(string path)
        {
            string filename = Path.GetFileName(path);
            File.Move(path, Path.Combine(destDir, filename));
        }

        static void invalidTorrentFile(string path)
        {
            string filename = Path.GetFileName(path);
            File.Move(path, Path.Combine(invalidDir, filename));
        }

        static string uploadTorrentFile(string path)
        {
            var client = new Client(url, null, username, password);

            NewTorrent nt = new NewTorrent();

            byte[] torrentContent = File.ReadAllBytes(path);

            nt.Metainfo = Convert.ToBase64String(torrentContent);

            NewTorrentInfo result = client.TorrentAdd(nt);

            string hash = result.HashString;

            if (hash.Length != 40)
                throw new Exception("Strange hash");

            return hash;

        }
    }
}
