﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using GitCommands.Logging;
using GitCommands.Repository;
using GitCommands.Settings;
using GitCommands.Utils;
using Microsoft.Win32;

namespace GitCommands
{
    public enum LocalChangesAction
    {
        DontChange,
        Merge,
        Reset,
        Stash
    }

    public static class AppSettings
    {
        //semi-constants
        public static readonly string GitExtensionsVersionString;
        public static readonly char PathSeparator = '\\';
        public static readonly char PathSeparatorWrong = '/';
        public static Version AppVersion { get { return Assembly.GetCallingAssembly().GetName().Version; } }
        public const string SettingsFileName = "GitExtensions.settings";

        public static Lazy<string> ApplicationDataPath;
        public static string SettingsFilePath { get { return Path.Combine(ApplicationDataPath.Value, SettingsFileName); } }

        private static SettingsContainer<RepoDistSettings> _SettingsContainer;
        public static SettingsContainer<RepoDistSettings> SettingsContainer { get { return _SettingsContainer; } }

        static AppSettings()
        {
            ApplicationDataPath = new Lazy<string>(() =>
            {
                if (IsPortable())
                {
                    return GetGitExtensionsDirectory();
                }
                else
                {
                    //Make applicationdatapath version independent
                    return Application.UserAppDataPath.Replace(Application.ProductVersion, string.Empty);
                }
            }
            );

            _SettingsContainer = new SettingsContainer<RepoDistSettings>(null, GitExtSettingsCache.FromCache(SettingsFilePath));
            Version version = AppVersion;

            GitExtensionsVersionString = version.Major.ToString() + '.' + version.Minor.ToString();
            if (version.Build > 0)
                GitExtensionsVersionString += '.' + version.Build.ToString();
            if (!EnvUtils.RunningOnWindows())
            {
                PathSeparator = '/';
                PathSeparatorWrong = '\\';
            }

            GitLog = new CommandLogger();

            if (!File.Exists(SettingsFilePath))
            {
                ImportFromRegistry();
            }
        }

        public static void UsingContainer(SettingsContainer<RepoDistSettings> aSettingsContainer, Action action)
        {
            SettingsContainer.LockedAction(() =>
                {
                    var oldSC = SettingsContainer;
                    try
                    {
                        _SettingsContainer = aSettingsContainer;
                        action();
                    }
                    finally
                    {
                        _SettingsContainer = oldSC;
                        //refresh settings if needed
                        SettingsContainer.GetString(string.Empty, null);
                    }
                }
             );
        }

        public static string GetInstallDir()
        {
#if DEBUG
            string gitExtDir = GetGitExtensionsDirectory().TrimEnd('\\').TrimEnd('/');
            string debugPath = @"GitExtensions\bin\Debug";
            int len = debugPath.Length;
            var path = gitExtDir.Substring(gitExtDir.Length - len);
            if (debugPath.Replace('\\', '/').Equals(path.Replace('\\', '/')))
            {
                string projectPath = gitExtDir.Substring(0, len + 2);
                return Path.Combine(projectPath, "Bin");
            }
#endif

            if (IsPortable())
            {
                return GetGitExtensionsDirectory();
            }
            else
            {
                return (string)VersionIndependentRegKey.GetValue("InstallDir", string.Empty);
            }
        }

        //for repair only
        public static void SetInstallDir(string dir)
        {
            VersionIndependentRegKey.SetValue("InstallDir", dir);
        }

        public static string CascadeShellMenuItems
        {
            get { return (string)VersionIndependentRegKey.GetValue("CascadeShellMenuItems", "110111000111111111"); }
            set { VersionIndependentRegKey.SetValue("CascadeShellMenuItems", value); }
        }

        public static bool AlwaysShowAllCommands
        {
            get { return (int)VersionIndependentRegKey.GetValue("AlwaysShowAllCommands", 0) != 0; }
            set { VersionIndependentRegKey.SetValue("AlwaysShowAllCommands", value ? 1 : 0); }
        }

        public static bool ShowCurrentBranchInVisualStudio
        {
            //This setting MUST be set to false by default, otherwise it will not work in Visual Studio without
            //other changes in the Visual Studio plugin itself.
            get { return (int)VersionIndependentRegKey.GetValue("showcurrentbranchinvisualstudio", 1) != 0; }
            set { VersionIndependentRegKey.SetValue("showcurrentbranchinvisualstudio", value ? 1 : 0); }
        }

        public static string GitCommand
        {
            get
            {
                if (IsPortable())
                    return GetString("gitcommand", "git");
                else
                    return (string)VersionIndependentRegKey.GetValue("gitcommand", "git");
            }
            set
            {
                if (IsPortable())
                    SetString("gitcommand", value);
                else
                    VersionIndependentRegKey.SetValue("gitcommand", value);
            }
        }

        public static int UserMenuLocationX
        {
            get { return GetInt("usermenulocationx", -1); }
            set { SetInt("usermenulocationx", value); }
        }

        public static int UserMenuLocationY
        {
            get { return GetInt("usermenulocationy", -1); }
            set { SetInt("usermenulocationy", value); }
        }

        public static bool StashKeepIndex
        {
            get { return GetBool("stashkeepindex", false); }
            set { SetBool("stashkeepindex", value); }
        }

        public static bool StashConfirmDropShow
        {
            get { return GetBool("stashconfirmdropshow", true); }
            set { SetBool("stashconfirmdropshow", value); }
        }

        public static bool ApplyPatchIgnoreWhitespace
        {
            get { return GetBool("applypatchignorewhitespace", false); }
            set { SetBool("applypatchignorewhitespace", value); }
        }

        public static bool UsePatienceDiffAlgorithm
        {
            get { return GetBool("usepatiencediffalgorithm", false); }
            set { SetBool("usepatiencediffalgorithm", value); }
        }

        public static bool ShowErrorsWhenStagingFiles
        {
            get { return GetBool("showerrorswhenstagingfiles", true); }
            set { SetBool("showerrorswhenstagingfiles", value); }
        }

        public static string LastCommitMessage
        {
            get { return GetString("lastCommitMessage", ""); }
            set { SetString("lastCommitMessage", value); }
        }

        public static string TruncatePathMethod
        {
            get { return GetString("truncatepathmethod", "none"); }
            set { SetString("truncatepathmethod", value); }
        }

        public static bool ShowGitStatusInBrowseToolbar
        {
            get { return GetBool("showgitstatusinbrowsetoolbar", true); }
            set { SetBool("showgitstatusinbrowsetoolbar", value); }
        }

        public static bool CommitInfoShowContainedInBranches
        {
            get
            {
                return CommitInfoShowContainedInBranchesLocal ||
                    CommitInfoShowContainedInBranchesRemote ||
                    CommitInfoShowContainedInBranchesRemoteIfNoLocal;
            }
        }

        public static bool CommitInfoShowContainedInBranchesLocal
        {
            get { return GetBool("commitinfoshowcontainedinbrancheslocal", true); }
            set { SetBool("commitinfoshowcontainedinbrancheslocal", value); }
        }

        public static bool CheckForUncommittedChangesInCheckoutBranch
        {
            get { return GetBool("checkforuncommittedchangesincheckoutbranch", false); }
            set { SetBool("checkforuncommittedchangesincheckoutbranch", value); }
        }

        public static bool AlwaysShowCheckoutBranchDlg
        {
            get { return GetBool("AlwaysShowCheckoutBranchDlg", false); }
            set { SetBool("AlwaysShowCheckoutBranchDlg", value); }
        }

        public static bool CommitInfoShowContainedInBranchesRemote
        {
            get { return GetBool("commitinfoshowcontainedinbranchesremote", false); }
            set { SetBool("commitinfoshowcontainedinbranchesremote", value); }
        }

        public static bool CommitInfoShowContainedInBranchesRemoteIfNoLocal
        {
            get { return GetBool("commitinfoshowcontainedinbranchesremoteifnolocal", false); }
            set { SetBool("commitinfoshowcontainedinbranchesremoteifnolocal", value); }
        }

        public static bool CommitInfoShowContainedInTags
        {
            get { return GetBool("commitinfoshowcontainedintags", true); }
            set { SetBool("commitinfoshowcontainedintags", value); }
        }

        public static string GravatarCachePath
        {
            get { return ApplicationDataPath.Value + "Images\\"; }
        }

        public static string Translation
        {
            get { return GetString("translation", ""); }
            set { SetString("translation", value); }
        }

        private static string _currentTranslation;
        public static string CurrentTranslation
        {
            get { return _currentTranslation ?? Translation; }
            set { _currentTranslation = value; }
        }

        public static bool UserProfileHomeDir
        {
            get { return GetBool("userprofilehomedir", false); }
            set { SetBool("userprofilehomedir", value); }
        }

        public static string CustomHomeDir
        {
            get { return GetString("customhomedir", ""); }
            set { SetString("customhomedir", value); }
        }

        public static bool EnableAutoScale
        {
            get { return GetBool("enableautoscale", true); }
            set { SetBool("enableautoscale", value); }
        }

        public static string IconColor
        {
            get { return GetString("iconcolor", "default"); }
            set { SetString("iconcolor", value); }
        }

        public static string IconStyle
        {
            get { return GetString("iconstyle", "default"); }
            set { SetString("iconstyle", value); }
        }

        public static int AuthorImageSize
        {
            get { return GetInt("authorimagesize", 80); }
            set { SetInt("authorimagesize", value); }
        }

        public static int AuthorImageCacheDays
        {
            get { return GetInt("authorimagecachedays", 5); }
            set { SetInt("authorimagecachedays", value); }
        }

        public static bool ShowAuthorGravatar
        {
            get { return GetBool("showauthorgravatar", true); }
            set { SetBool("showauthorgravatar", value); }
        }

        public static bool CloseCommitDialogAfterCommit
        {
            get { return GetBool("closecommitdialogaftercommit", true); }
            set { SetBool("closecommitdialogaftercommit", value); }
        }

        public static bool CloseCommitDialogAfterLastCommit
        {
            get { return GetBool("closecommitdialogafterlastcommit", true); }
            set { SetBool("closecommitdialogafterlastcommit", value); }
        }

        public static bool RefreshCommitDialogOnFormFocus
        {
            get { return GetBool("refreshcommitdialogonformfocus", false); }
            set { SetBool("refreshcommitdialogonformfocus", value); }
        }

        public static bool StageInSuperprojectAfterCommit
        {
            get { return GetBool("stageinsuperprojectaftercommit", true); }
            set { SetBool("stageinsuperprojectaftercommit", value); }
        }

        public static bool PlaySpecialStartupSound
        {
            get { return GetBool("PlaySpecialStartupSound", false); }
            set { SetBool("PlaySpecialStartupSound", value); }
        }

        public static bool FollowRenamesInFileHistory
        {
            get { return GetBool("followrenamesinfilehistory", true); }
            set { SetBool("followrenamesinfilehistory", value); }
        }

        public static bool FullHistoryInFileHistory
        {
            get { return GetBool("fullhistoryinfilehistory", false); }
            set { SetBool("fullhistoryinfilehistory", value); }
        }

        public static bool LoadFileHistoryOnShow
        {
            get { return GetBool("LoadFileHistoryOnShow", true); }
            set { SetBool("LoadFileHistoryOnShow", value); }
        }

        public static bool LoadBlameOnShow
        {
            get { return GetBool("LoadBlameOnShow", true); }
            set { SetBool("LoadBlameOnShow", value); }
        }

        public static bool RevisionGraphShowWorkingDirChanges
        {
            get { return GetBool("revisiongraphshowworkingdirchanges", false); }
            set { SetBool("revisiongraphshowworkingdirchanges", value); }
        }

        public static bool RevisionGraphDrawNonRelativesGray
        {
            get { return GetBool("revisiongraphdrawnonrelativesgray", true); }
            set { SetBool("revisiongraphdrawnonrelativesgray", value); }
        }

        public static bool RevisionGraphDrawNonRelativesTextGray
        {
            get { return GetBool("revisiongraphdrawnonrelativestextgray", false); }
            set { SetBool("revisiongraphdrawnonrelativestextgray", value); }
        }

        public static readonly Dictionary<string, Encoding> AvailableEncodings = new Dictionary<string, Encoding>();

        public enum PullAction
        {
            None,
            Merge,
            Rebase,
            Fetch,
            FetchAll,
            Default
        }

        public static PullAction FormPullAction
        {
            get { return GetEnum("FormPullAction", PullAction.Merge); }
            set { SetEnum("FormPullAction", value); }
        }

        public static bool DonSetAsLastPullAction
        {
            get { return GetBool("DonSetAsLastPullAction", true); }
            set { SetBool("DonSetAsLastPullAction", value); }
        }

        public static string SmtpServer
        {
            get { return GetString("SmtpServer", "smtp.gmail.com"); }
            set { SetString("SmtpServer", value); }
        }

        public static int SmtpPort
        {
            get { return GetInt("SmtpPort", 465); }
            set { SetInt("SmtpPort", value); }
        }

        public static bool SmtpUseSsl
        {
            get { return GetBool("SmtpUseSsl", true); }
            set { SetBool("SmtpUseSsl", value); }
        }

        public static bool AutoStash
        {
            get { return GetBool("autostash", false); }
            set { SetBool("autostash", value); }
        }

        public static LocalChangesAction CheckoutBranchAction
        {
            get { return GetEnum("checkoutbranchaction", LocalChangesAction.DontChange); }
            set { SetEnum("checkoutbranchaction", value); }
        }

        public static bool UseDefaultCheckoutBranchAction
        {
            get { return GetBool("UseDefaultCheckoutBranchAction", false); }
            set { SetBool("UseDefaultCheckoutBranchAction", value); }
        }

        public static bool DontShowHelpImages
        {
            get { return GetBool("DontShowHelpImages", false); }
            set { SetBool("DontShowHelpImages", value); }
        }

        public static bool AlwaysShowAdvOpt
        {
            get { return GetBool("AlwaysShowAdvOpt", false); }
            set { SetBool("AlwaysShowAdvOpt", value); }
        }

        public static bool DontConfirmAmend
        {
            get { return GetBool("DontConfirmAmend", false); }
            set { SetBool("DontConfirmAmend", value); }
        }

        public static bool? AutoPopStashAfterPull
        {
            get { return GetBool("AutoPopStashAfterPull"); }
            set { SetBool("AutoPopStashAfterPull", value); }
        }

        public static bool? AutoPopStashAfterCheckoutBranch
        {
            get { return GetBool("AutoPopStashAfterCheckoutBranch"); }
            set { SetBool("AutoPopStashAfterCheckoutBranch", value); }
        }

        public static PullAction? AutoPullOnPushRejectedAction
        {
            get { return GetNullableEnum<PullAction>("AutoPullOnPushRejectedAction"); }
            set { SetNullableEnum<PullAction>("AutoPullOnPushRejectedAction", value); }
        }

        public static bool DontConfirmPushNewBranch
        {
            get { return GetBool("DontConfirmPushNewBranch", false); }
            set { SetBool("DontConfirmPushNewBranch", value); }
        }

        public static bool DontConfirmAddTrackingRef
        {
            get { return GetBool("DontConfirmAddTrackingRef", false); }
            set { SetBool("DontConfirmAddTrackingRef", value); }
        }

        public static bool IncludeUntrackedFilesInAutoStash
        {
            get { return GetBool("includeUntrackedFilesInAutoStash", true); }
            set { SetBool("includeUntrackedFilesInAutoStash", value); }
        }

        public static bool IncludeUntrackedFilesInManualStash
        {
            get { return GetBool("includeUntrackedFilesInManualStash", true); }
            set { SetBool("includeUntrackedFilesInManualStash", value); }
        }

        public static bool OrderRevisionByDate
        {
            get { return GetBool("orderrevisionbydate", true); }
            set { SetBool("orderrevisionbydate", value); }
        }

        public static string Dictionary
        {
            get { return GetString("dictionary", "en-US"); }
            set { SetString("dictionary", value); }
        }

        public static bool ShowGitCommandLine
        {
            get { return GetBool("showgitcommandline", false); }
            set { SetBool("showgitcommandline", value); }
        }

        public static bool ShowStashCount
        {
            get { return GetBool("showstashcount", false); }
            set { SetBool("showstashcount", value); }
        }

        public static bool RelativeDate
        {
            get { return GetBool("relativedate", true); }
            set { SetBool("relativedate", value); }
        }

        public static bool UseFastChecks
        {
            get { return GetBool("usefastchecks", false); }
            set { SetBool("usefastchecks", value); }
        }

        public static bool ShowGitNotes
        {
            get { return GetBool("showgitnotes", false); }
            set { SetBool("showgitnotes", value); }
        }

        public static bool ShowIndicatorForMultilineMessage
        {
            get { return GetBool("showindicatorformultilinemessage", false); }
            set { SetBool("showindicatorformultilinemessage", value); }
        }

        public static bool ShowMergeCommits
        {
            get { return GetBool("showmergecommits", true); }
            set { SetBool("showmergecommits", value); }
        }

        public static bool ShowTags
        {
            get { return GetBool("showtags", true); }
            set { SetBool("showtags", value); }
        }

        public static int RevisionGraphLayout
        {
            get { return GetInt("revisiongraphlayout", 2); }
            set { SetInt("revisiongraphlayout", value); }
        }

        public static bool ShowAuthorDate
        {
            get { return GetBool("showauthordate", true); }
            set { SetBool("showauthordate", value); }
        }

        public static bool CloseProcessDialog
        {
            get { return GetBool("closeprocessdialog", false); }
            set { SetBool("closeprocessdialog", value); }
        }

        public static bool ShowCurrentBranchOnly
        {
            get { return GetBool("showcurrentbranchonly", false); }
            set { SetBool("showcurrentbranchonly", value); }
        }

        public static bool BranchFilterEnabled
        {
            get { return GetBool("branchfilterenabled", false); }
            set { SetBool("branchfilterenabled", value); }
        }

        public static int CommitDialogSplitter
        {
            get { return GetInt("commitdialogsplitter", -1); }
            set { SetInt("commitdialogsplitter", value); }
        }

        public static int CommitDialogRightSplitter
        {
            get { return GetInt("commitdialogrightsplitter", -1); }
            set { SetInt("commitdialogrightsplitter", value); }
        }

        public static string DefaultCloneDestinationPath
        {
            get { return GetString("defaultclonedestinationpath", string.Empty); }
            set { SetString("defaultclonedestinationpath", value); }
        }

        public static int RevisionGridQuickSearchTimeout
        {
            get { return GetInt("revisiongridquicksearchtimeout", 750); }
            set { SetInt("revisiongridquicksearchtimeout", value); }
        }

        public static string GravatarFallbackService
        {
            get { return GetString("gravatarfallbackservice", "Identicon"); }
            set { SetString("gravatarfallbackservice", value); }
        }

        public static string GitBinDir
        {
            get { return GetString("gitbindir", ""); }
            set
            {
                var temp = value;
                if (temp.Length > 0 && temp[temp.Length - 1] != PathSeparator)
                    temp += PathSeparator;
                SetString("gitbindir", temp);

                //if (string.IsNullOrEmpty(_gitBinDir))
                //    return;

                //var path =
                //    Environment.GetEnvironmentVariable("path", EnvironmentVariableTarget.Process) + ";" +
                //    _gitBinDir;
                //Environment.SetEnvironmentVariable("path", path, EnvironmentVariableTarget.Process);
            }
        }

        public static int MaxRevisionGraphCommits
        {
            get { return GetInt("maxrevisiongraphcommits", 100000); }
            set { SetInt("maxrevisiongraphcommits", value); }
        }

        public static string RecentWorkingDir
        {
            get { return GetString("RecentWorkingDir", null); }
            set { SetString("RecentWorkingDir", value); }
        }

        public static bool StartWithRecentWorkingDir
        {
            get { return GetBool("StartWithRecentWorkingDir", false); }
            set { SetBool("StartWithRecentWorkingDir", value); }
        }

        public static CommandLogger GitLog { get; private set; }

        public static string Plink
        {
            get { return GetString("plink", ""); }
            set { SetString("plink", value); }
        }
        public static string Puttygen
        {
            get { return GetString("puttygen", ""); }
            set { SetString("puttygen", value); }
        }

        public static string Pageant
        {
            get { return GetString("pageant", ""); }
            set { SetString("pageant", value); }
        }

        public static bool AutoStartPageant
        {
            get { return GetBool("autostartpageant", true); }
            set { SetBool("autostartpageant", value); }
        }

        public static bool MarkIllFormedLinesInCommitMsg
        {
            get { return GetBool("markillformedlinesincommitmsg", false); }
            set { SetBool("markillformedlinesincommitmsg", value); }
        }

        #region Colors

        public static Color OtherTagColor
        {
            get { return GetColor("othertagcolor", Color.Gray); }
            set { SetColor("othertagcolor", value); }
        }

        public static Color TagColor
        {
            get { return GetColor("tagcolor", Color.DarkBlue); }
            set { SetColor("tagcolor", value); }
        }

        public static Color GraphColor
        {
            get { return GetColor("graphcolor", Color.DarkRed); }
            set { SetColor("graphcolor", value); }
        }

        public static Color BranchColor
        {
            get { return GetColor("branchcolor", Color.DarkRed); }
            set { SetColor("branchcolor", value); }
        }

        public static Color RemoteBranchColor
        {
            get { return GetColor("remotebranchcolor", Color.Green); }
            set { SetColor("remotebranchcolor", value); }
        }

        public static Color DiffSectionColor
        {
            get { return GetColor("diffsectioncolor", Color.FromArgb(230, 230, 230)); }
            set { SetColor("diffsectioncolor", value); }
        }

        public static Color DiffRemovedColor
        {
            get { return GetColor("diffremovedcolor", Color.FromArgb(255, 200, 200)); }
            set { SetColor("diffremovedcolor", value); }
        }

        public static Color DiffRemovedExtraColor
        {
            get { return GetColor("diffremovedextracolor", Color.FromArgb(255, 150, 150)); }
            set { SetColor("diffremovedextracolor", value); }
        }

        public static Color DiffAddedColor
        {
            get { return GetColor("diffaddedcolor", Color.FromArgb(200, 255, 200)); }
            set { SetColor("diffaddedcolor", value); }
        }

        public static Color DiffAddedExtraColor
        {
            get { return GetColor("diffaddedextracolor", Color.FromArgb(135, 255, 135)); }
            set { SetColor("diffaddedextracolor", value); }
        }

        public static Font DiffFont
        {
            get { return GetFont("difffont", new Font("Courier New", 10)); }
            set { SetFont("difffont", value); }
        }

        public static Font CommitFont
        {
            get { return GetFont("commitfont", new Font(SystemFonts.MessageBoxFont.Name, SystemFonts.MessageBoxFont.Size)); }
            set { SetFont("commitfont", value); }
        }

        public static Font Font
        {
            get { return GetFont("font", new Font(SystemFonts.MessageBoxFont.Name, SystemFonts.MessageBoxFont.Size)); }
            set { SetFont("font", value); }
        }

        #endregion

        public static bool MulticolorBranches
        {
            get { return GetBool("multicolorbranches", true); }
            set { SetBool("multicolorbranches", value); }
        }

        public static bool StripedBranchChange
        {
            get { return GetBool("stripedbranchchange", true); }
            set { SetBool("stripedbranchchange", value); }
        }

        public static bool BranchBorders
        {
            get { return GetBool("branchborders", true); }
            set { SetBool("branchborders", value); }
        }

        public static string LastFormatPatchDir
        {
            get { return GetString("lastformatpatchdir", ""); }
            set { SetString("lastformatpatchdir", value); }
        }

        public static string GetDictionaryDir()
        {
            return Path.Combine(GetInstallDir(), "Dictionaries");
        }

        public static void SaveSettings()
        {
            try
            {
                SettingsContainer.LockedAction(() =>
                {
                    SetString("gitssh", GitCommandHelpers.GetSsh());
                    Repositories.SaveSettings();

                    SettingsContainer.Save();
                });
            }
            catch
            { }
        }

        public static void LoadSettings()
        {
            Action<Encoding> addEncoding = delegate(Encoding e) { AvailableEncodings[e.HeaderName] = e; };
            addEncoding(Encoding.Default);
            addEncoding(new ASCIIEncoding());
            addEncoding(new UnicodeEncoding());
            addEncoding(new UTF7Encoding());
            addEncoding(new UTF8Encoding(false));

            try
            {
                GitCommandHelpers.SetSsh(GetString("gitssh", null));
            }
            catch
            { }
        }

        public static bool DashboardShowCurrentBranch
        {
            get { return GetBool("dashboardshowcurrentbranch", true); }
            set { SetBool("dashboardshowcurrentbranch", value); }
        }

        public static string ownScripts
        {
            get { return GetString("ownScripts", ""); }
            set { SetString("ownScripts", value); }
        }

        public static int RecursiveSubmodules
        {
            get { return GetInt("RecursiveSubmodules", 1); }
            set { SetInt("RecursiveSubmodules", value); }
        }

        public static string ShorteningRecentRepoPathStrategy
        {
            get { return GetString("ShorteningRecentRepoPathStrategy", ""); }
            set { SetString("ShorteningRecentRepoPathStrategy", value); }
        }

        public static int MaxMostRecentRepositories
        {
            get { return GetInt("MaxMostRecentRepositories", 0); }
            set { SetInt("MaxMostRecentRepositories", value); }
        }

        public static int RecentReposComboMinWidth
        {
            get { return GetInt("RecentReposComboMinWidth", 0); }
            set { SetInt("RecentReposComboMinWidth", value); }
        }

        public static bool SortMostRecentRepos
        {
            get { return GetBool("SortMostRecentRepos", false); }
            set { SetBool("SortMostRecentRepos", value); }
        }

        public static bool SortLessRecentRepos
        {
            get { return GetBool("SortLessRecentRepos", false); }
            set { SetBool("SortLessRecentRepos", value); }
        }

        public static bool DontCommitMerge
        {
            get { return GetBool("DontCommitMerge", false); }
            set { SetBool("DontCommitMerge", value); }
        }

        public static int CommitValidationMaxCntCharsFirstLine
        {
            get { return GetInt("CommitValidationMaxCntCharsFirstLine", 0); }
            set { SetInt("CommitValidationMaxCntCharsFirstLine", value); }
        }

        public static int CommitValidationMaxCntCharsPerLine
        {
            get { return GetInt("CommitValidationMaxCntCharsPerLine", 0); }
            set { SetInt("CommitValidationMaxCntCharsPerLine", value); }
        }

        public static bool CommitValidationSecondLineMustBeEmpty
        {
            get { return GetBool("CommitValidationSecondLineMustBeEmpty", false); }
            set { SetBool("CommitValidationSecondLineMustBeEmpty", value); }
        }

        public static bool CommitValidationIndentAfterFirstLine
        {
            get { return GetBool("CommitValidationIndentAfterFirstLine", true); }
            set { SetBool("CommitValidationIndentAfterFirstLine", value); }
        }

        public static bool CommitValidationAutoWrap
        {
            get { return GetBool("CommitValidationAutoWrap", true); }
            set { SetBool("CommitValidationAutoWrap", value); }
        }

        public static string CommitValidationRegEx
        {
            get { return GetString("CommitValidationRegEx", String.Empty); }
            set { SetString("CommitValidationRegEx", value); }
        }

        public static string CommitTemplates
        {
            get { return GetString("CommitTemplates", String.Empty); }
            set { SetString("CommitTemplates", value); }
        }

        public static bool CreateLocalBranchForRemote
        {
            get { return GetBool("CreateLocalBranchForRemote", false); }
            set { SetBool("CreateLocalBranchForRemote", value); }
        }

        public static bool UseFormCommitMessage
        {
            get { return GetBool("UseFormCommitMessage", true); }
            set { SetBool("UseFormCommitMessage", value); }
        }

        public static DateTime LastUpdateCheck
        {
            get { return GetDate("LastUpdateCheck", default(DateTime)); }
            set { SetDate("LastUpdateCheck", value); }
        }

        public static bool ShowResetAllButton
        {
            get { return GetBool("ShowResetAllButton", false); ; }
            set { SetBool("ShowResetAllButton", value); }
        }
        
        public static string GetGitExtensionsFullPath()
        {
            return Application.ExecutablePath;
        }

        public static string GetGitExtensionsDirectory()
        {
            return Path.GetDirectoryName(GetGitExtensionsFullPath());
        }

        private static RegistryKey _VersionIndependentRegKey;

        private static RegistryKey VersionIndependentRegKey
        {
            get
            {
                if (_VersionIndependentRegKey == null)
                    _VersionIndependentRegKey = Registry.CurrentUser.CreateSubKey("Software\\GitExtensions", RegistryKeyPermissionCheck.ReadWriteSubTree);
                return _VersionIndependentRegKey;
            }
        }

        public static bool IsPortable()
        {
            return Properties.Settings.Default.IsPortable;

        }

        private static IEnumerable<Tuple<string, string>> GetSettingsFromRegistry()
        {
            foreach (String name in VersionIndependentRegKey.GetValueNames())
            {
                object value = VersionIndependentRegKey.GetValue(name, null);
                if (value != null)
                    yield return new Tuple<string, string>(name, value.ToString());
            }
        }

        private static void ImportFromRegistry()
        {
            SettingsContainer.SettingsCache.Import(GetSettingsFromRegistry());
        }

        public static string PrefixedName(string prefix, string name)
        {
            return prefix == null ? name : prefix + '_' + name;
        }

        public static bool? GetBool(string name)
        {
            return SettingsContainer.GetBool(name);
        }

        public static bool GetBool(string name, bool defaultValue)
        {
            return SettingsContainer.GetBool(name, defaultValue);
        }

        public static void SetBool(string name, bool? value)
        {
            SettingsContainer.SetBool(name, value);
        }

        public static void SetInt(string name, int? value)
        {
            SettingsContainer.SetInt(name, value);
        }

        public static int? GetInt(string name)
        {
            return SettingsContainer.GetInt(name);
        }

        public static DateTime GetDate(string name, DateTime defaultValue)
        {
            return SettingsContainer.GetDate(name, defaultValue);
        }

        public static void SetDate(string name, DateTime? value)
        {
            SettingsContainer.SetDate(name, value);
        }

        public static DateTime? GetDate(string name)
        {
            return SettingsContainer.GetDate(name);
        }

        public static int GetInt(string name, int defaultValue)
        {
            return SettingsContainer.GetInt(name, defaultValue);
        }

        public static void SetFont(string name, Font value)
        {
            SettingsContainer.SetFont(name, value);
        }

        public static Font GetFont(string name, Font defaultValue)
        {
            return SettingsContainer.GetFont(name, defaultValue);
        }

        public static void SetColor(string name, Color? value)
        {
            SettingsContainer.SetColor(name, value);
        }

        public static Color? GetColor(string name)
        {
            return SettingsContainer.GetColor(name);
        }

        public static Color GetColor(string name, Color defaultValue)
        {
            return SettingsContainer.GetColor(name, defaultValue);
        }

        public static void SetEnum<T>(string name, T value)
        {
            SettingsContainer.SetEnum(name, value);
        }

        public static T GetEnum<T>(string name, T defaultValue) where T : struct
        {
            return SettingsContainer.GetEnum(name, defaultValue);
        }

        public static void SetNullableEnum<T>(string name, T? value) where T : struct
        {
            SettingsContainer.SetNullableEnum(name, value);
        }

        public static T? GetNullableEnum<T>(string name) where T : struct
        {
            return SettingsContainer.GetNullableEnum<T>(name);
        }

        public static void SetString(string name, string value)
        {
            SettingsContainer.SetString(name, value);
        }

        public static string GetString(string name, string defaultValue)
        {
            return SettingsContainer.GetString(name, defaultValue);
        }

    }

    /*
    public abstract class Setting<T> : IXmlSerializable
    {
        public SettingsContainer SettingsSource { get; private set; }
        private T _Value;
        public bool HasValue { get; private set; }
        public string Name { get; private set; }
        public T DefaultValue { get; private set; }

        public Setting(SettingsContainer aSettingsSource, string aName, T aDefaultValue)
        {
            SettingsSource = aSettingsSource;
            Name = aName;
            DefaultValue = aDefaultValue;
            HasValue = false;
        }

        public T Value
        {
            get
            {
                return SettingsSource.GetValue(Name, DefaultValue, (s) => DefaultValue);
            }

            set
            {
                //SettingsSource.SetValue(this, value);
            }
        }

        public virtual System.Xml.Schema.XmlSchema GetSchema()
        {
            return null;
        }

        public abstract void ReadXml(XmlReader reader);
        public abstract void WriteXml(XmlWriter writer);
    }

    */

}
