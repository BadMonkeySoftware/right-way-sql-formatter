/*
Poor Man's T-SQL Formatter - a small free Transact-SQL formatting
library for .Net 2.0 and JS, written in C#.
Copyright (C) 2011-2017 Tao Klerks

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.

*/

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using PoorMansTSqlFormatterSSMSLib;
using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;

// This is the SSMS 21/22 (VS 2022 / VS17 shell) variant of the package. It differs
// from the SSMS18 package ONLY in its loading model: VS17 REFUSES synchronous autoload
// (ActivityLog "AutoLoadManager: ... ignored because package does not support background
// loading" + "SyncAutoLoadedExtensions: ... synchronous autoload ... is deprecated"), so
// this package is an AsyncPackage with AllowsBackgroundLoading + BackgroundLoad autoload.
// All the real work still lives in the shell-agnostic SSMSLib. Distinct package/cmdset
// GUIDs keep it independent from the SSMS18 registration.
namespace PoorMansTSqlFormatterSSMSPackage
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]  //General VSPackage hookup; AllowsBackgroundLoading is required for VS17 async load
    [InstalledProductRegistration("#ProductName", "#ProductDescription", "1.6.16")]  //Package Medatada, references to VSPackage.resx resource keys
    [ProvideAutoLoad(VSConstants.UICONTEXT.NotBuildingAndNotDebugging_string, PackageAutoLoadFlags.BackgroundLoad)] // Background auto-load for dynamic menu enabling/disabling; VS17 ignores sync autoload
    [ProvideMenuResource("Menus.ctmenu", 1)]  //Hook to command definitions / to vsct stuff
    [Guid(guidPoorMansTSqlFormatterSSMSPackagePkgString)] //Arbitrarily/randomly defined guid for this extension
    public sealed class FormatterPackage : AsyncPackage
    {
        //These constants are duplicated in the vsct file
        public const string guidPoorMansTSqlFormatterSSMSPackagePkgString = "e857c020-26ea-4b6f-b0d0-d97fb572ee81";
        public const string guidPoorMansTSqlFormatterSSMSPackageCmdSetString = "1a2afa6c-2336-4041-8763-35790feae5d0";
        public const uint cmdidPoorMansFormatSQL = 0x100;
        public const uint cmdidPoorMansSqlOptions = 0x101;

        public static readonly Guid guidPoorMansTSqlFormatterSSMSPackageCmdSet = new Guid(guidPoorMansTSqlFormatterSSMSPackageCmdSetString);

        private GenericVSHelper _SSMSHelper;

        public FormatterPackage()
        {
        }

        protected override async System.Threading.Tasks.Task InitializeAsync(System.Threading.CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await base.InitializeAsync(cancellationToken, progress);

            _SSMSHelper = new GenericVSHelper(true, null, null, null);

            //Switch to UI thread, so that we're allowed to get services
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Add our command handlers for the menu commands defined in the in the .vsct file, and enable them
            if (await GetServiceAsync(typeof(IMenuCommandService)).ConfigureAwait(false) is OleMenuCommandService mcs)
            {
                CommandID menuCommandID;
                OleMenuCommand menuCommand;

                // Create the formatting command / menu item.
                menuCommandID = new CommandID(guidPoorMansTSqlFormatterSSMSPackageCmdSet, (int)cmdidPoorMansFormatSQL);
                menuCommand = new OleMenuCommand(FormatSqlCallback, menuCommandID);
                mcs.AddCommand(menuCommand);
                menuCommand.BeforeQueryStatus += new EventHandler(QueryFormatButtonStatus);

                // Create the options command / menu item.
                menuCommandID = new CommandID(guidPoorMansTSqlFormatterSSMSPackageCmdSet, (int)cmdidPoorMansSqlOptions);
                menuCommand = new OleMenuCommand(SqlOptionsCallback, menuCommandID);
                menuCommand.Enabled = true;
                mcs.AddCommand(menuCommand);
            }

            return;
        }

        private void FormatSqlCallback(object sender, EventArgs e)
        {
            DTE2 dte = (DTE2)GetService(typeof(DTE));
            _SSMSHelper.FormatSqlInTextDoc(dte);
        }

        private void SqlOptionsCallback(object sender, EventArgs e)
        {
            _SSMSHelper.GetUpdatedFormattingOptionsFromUser();
        }

        private void QueryFormatButtonStatus(object sender, EventArgs e)
        {
            var queryingCommand = sender as OleMenuCommand;
            DTE2 dte = (DTE2)GetService(typeof(DTE));
            if (queryingCommand != null && dte.ActiveDocument != null && !dte.ActiveDocument.ReadOnly)
                queryingCommand.Enabled = true;
            else
                queryingCommand.Enabled = false;
        }
    }
}
