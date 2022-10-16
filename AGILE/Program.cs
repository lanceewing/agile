using System;
using System.IO;
using System.Windows.Forms;

namespace AGILE
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Start by attempting to run with command line argument or the current folder.
            Boolean stillChoosingGame = true, firstTime = true;
            string gameFolder = (args.Length > 0? args[0] : Directory.GetCurrentDirectory());
            AGI.Game game = null;

            while (stillChoosingGame)
            {
                try
                {
                    game = new AGI.Game(gameFolder);
                    stillChoosingGame = false;
                }
                catch (Exception)
                {
                    // There isn't an AGI game in the current folder, so ask player to choose a different folder.
                    using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                    {
                        string prompt = "Please choose a folder containing an AGI game.";
                        
                        folderDialog.ShowNewFolderButton = false;
                        folderDialog.RootFolder = Environment.SpecialFolder.MyComputer;
                        folderDialog.ShowNewFolderButton = false;
                        folderDialog.Description = prompt;
                        //folderDialog.SelectedPath = "E:\\qGames\\AGI Games";
                        folderDialog.SelectedPath = Properties.Settings.Default.lastBrowsePath;

                        if (!firstTime)
                        {
                            prompt = $"No AGI game was found in {gameFolder}\n\n{prompt}";
                            folderDialog.SelectedPath = gameFolder;
                            folderDialog.SelectedPath = Properties.Settings.Default.lastBrowsePath;
                            Properties.Settings.Default.Save();
                        }

                        // Tiny hack here to force the Folder Selection dialog to the front. It needs to be associated with a window.
                        using (var dummyForm = new Form() { TopMost = true })
                        {
                            DialogResult result = folderDialog.ShowDialog(Control.FromHandle(dummyForm.Handle));
                            if (result == DialogResult.OK)
                            {
                                Properties.Settings.Default.lastBrowsePath = folderDialog.SelectedPath;
                                Properties.Settings.Default.Save();

                                gameFolder = folderDialog.SelectedPath;
                                firstTime = false;
                            }
                            else if (result == DialogResult.Cancel)
                            {
                                Environment.Exit(0);
                            }
                        }
                    }
                }
            }

            if (game != null)
            {
                Application.Run(new AgileForm(game));
            }
        }
    }
}
