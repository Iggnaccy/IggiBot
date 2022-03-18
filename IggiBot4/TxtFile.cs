using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading.Tasks;

namespace IggiBot4
{
    class TxtFile
    {
        string path;

        public TxtFile(string Path)
        {
            path = Path;
        }

        public List<string> ReadAllLines()
        {
            StreamReader sr = new StreamReader(path);
            List<string> r = new List<string>();
            while(!sr.EndOfStream)
            {
                r.Add(sr.ReadLine());
            }
            sr.Close();
            return r;
        }

        public async Task<List<string>> ReadAllLinesAsync()
        {
            StreamReader sr = new StreamReader(path);
            List<string> r = new List<string>();
            while(!sr.EndOfStream)
            {
                r.Add(await sr.ReadLineAsync());
            }
            sr.Close();
            return r;
        }

        public void WriteLine(string text, bool append = false)
        {
            StreamWriter sw = new StreamWriter(path, append);
            sw.WriteLine(text);
            sw.Close();
        }

        public async Task WriteLineAsync(string text, bool append = false)
        {
            StreamWriter sw = new StreamWriter(path, append);
            await sw.WriteLineAsync(text);
            sw.Close();
        }

        public void WriteAllLines(List<string> lines, bool append = false)
        {
            StreamWriter sw = new StreamWriter(path, append);
            foreach(var line in lines)
            {
                sw.WriteLine(line);
            }
            sw.Close();
        }

        public async Task WriteAllLinesAsync(List<string> lines, bool append = false)
        {
            StreamWriter sw = new StreamWriter(path, append);
            foreach (var line in lines)
            {
                await sw.WriteLineAsync(line);
            }
            sw.Close();
        }
    }
}
