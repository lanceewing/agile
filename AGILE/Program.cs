using System;
using System.Windows.Forms;

namespace AGILE
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\AGIStudio138b1\\savegame")));
            Application.Run(new AgileForm(new AGI.Game("C:\\games\\kq1")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\kq2")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\kq3")));
            //Application.Run(new AgileForm(n/ew AGI.Game("E:\\games\\sq1")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\sq2")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\pq1")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\mumg")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\ddp")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\bc")));
            //Application.Run(new AgileForm(new AGI.Game("C:\\games\\ruby")));

            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\gr")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\kq4agi")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\mh")));
            //Application.Run(new AgileForm(new AGI.Game("E:\\games\\mh2")));

        }
    }
}
