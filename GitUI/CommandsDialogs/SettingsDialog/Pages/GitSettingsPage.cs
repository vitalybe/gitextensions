﻿using System;
using System.Diagnostics;
using System.Windows.Forms;
using GitCommands;
using ResourceManager.Translation;

namespace GitUI.CommandsDialogs.SettingsDialog.Pages
{
    public partial class GitSettingsPage : SettingsPageWithHeader
    {
        private readonly TranslationString _homeIsSetToString = new TranslationString("HOME is set to:");

        public GitSettingsPage()
        {
            InitializeComponent();
            Text = "Git";
            Translate();
        }

        protected override string GetCommaSeparatedKeywordList()
        {
            return "path,home,environment,variable,msys,cygwin,download,git,command,linux,tools";
        }

        public static SettingsPageReference GetPageReference()
        {
            return new SettingsPageReferenceByType(typeof(GitSettingsPage));
        }

        public override void OnPageShown()
        {
            GitPath.Text = AppSettings.GitCommand;
            GitBinPath.Text = AppSettings.GitBinDir;
        }

        protected override void SettingsToPage()
        {
            GitCommandHelpers.SetEnvironmentVariable();
            homeIsSetToLabel.Text = string.Concat(_homeIsSetToString.Text, " ", GitCommandHelpers.GetHomeDir());

            GitPath.Text = AppSettings.GitCommand;
            GitBinPath.Text = AppSettings.GitBinDir;
        }

        protected override void PageToSettings()
        {
            AppSettings.GitCommand = GitPath.Text;
            AppSettings.GitBinDir = GitBinPath.Text;
        }

        private void BrowseGitPath_Click(object sender, EventArgs e)
        {
            CheckSettingsLogic.SolveGitCommand();

            using (var browseDialog = new OpenFileDialog
            {
                FileName = AppSettings.GitCommand,
                Filter = "Git.cmd (git.cmd)|git.cmd|Git.exe (git.exe)|git.exe|Git (git)|git"
            })
            {

                if (browseDialog.ShowDialog(this) == DialogResult.OK)
                {
                    GitPath.Text = browseDialog.FileName;
                }
            }
        }

        private void BrowseGitBinPath_Click(object sender, EventArgs e)
        {
            CheckSettingsLogic.SolveLinuxToolsDir();

            using (var browseDialog = new FolderBrowserDialog { SelectedPath = AppSettings.GitBinDir })
            {

                if (browseDialog.ShowDialog(this) == DialogResult.OK)
                {
                    GitBinPath.Text = browseDialog.SelectedPath;
                }
            }
        }

        // TODO: needed anymore?
        private void GitPath_TextChanged(object sender, EventArgs e)
        {
            ////    if (loadingSettings)
            ////        return;

            ////    Settings.GitCommand = GitPath.Text;
            ////    OnLoadSettings();
        }

        private void downloadMsysgit_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start(@"http://msysgit.github.com/");
        }

        private void ChangeHomeButton_Click(object sender, EventArgs e)
        {
            PageHost.SaveAll();
            using (var frm = new FormFixHome()) frm.ShowDialog(this);
            PageHost.LoadAll();
            // TODO?: rescan

            // orginal:
            ////            throw new NotImplementedException(@"
            ////            Save();
            ////            using (var frm = new FormFixHome()) frm.ShowDialog(this);
            ////            LoadSettings();
            ////            Rescan_Click(null, null);
            ////            ");
        }
    }
}
