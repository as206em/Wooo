using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Diagnostics;

namespace EditMod
{
    static class Program
    {


        //this app just take the parameter passed from Main App
        //and store in settings file 
        //with Administrator privilege
        static void Main(string[] args)
        {
            try
            {
                Thread.Sleep(1000);

                int lenght = 0;
                String x = "";
                foreach (var item in args)
                {
                    x += item;
                    x += " ";
                }

                List<String> Data = x.Split(';').ToList();

                lenght = int.Parse(Data[0]) * 2;
                Data.RemoveAt(0);

                File.WriteAllLines("Setting.SSC", Data.Take(lenght));

                Data.RemoveRange(0, lenght);

                foreach (var item in Data)
                {
                    File.Copy(item, @"images/" + Path.GetFileName(item));
                }

                Process.Start("SniperShortCut.exe");
            }
            catch
            {
                try
                {
                    Process.Start("WoowShortCuts.exe");
                }
                catch
                {
                    MessageBox.Show("EEROR Can't open program again", "Sniper ShortCut", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

        }
    }
}
