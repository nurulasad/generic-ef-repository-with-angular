using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Zebra.Utilities;

namespace Zebra.DAL.Core.Test
{
	[TestClass]
	public class AlterScriptValidator : BaseDALTestCase
	{
		// There are lots of hard coded variables and paths in this test.
		// The reason is that these won't generally be useful elsewhere, and additionally are unlikely to change.
		private const string TABLE_SCRIPT_FILE = "/Zebra/DAL/Database/Schema/Create/Tables.sql";
		private const string ALTER_SCRIPT_DIRECTORY = "/Zebra/DAL/Database/Schema/Alter";
		private const string FIRST_ALTER_SCRIPT = "0.24.1.1.sql";


		[TestMethod]
		public void RunAndValidateAlterScripts()
		{
			// Get list of alter scripts, and sort correctly
			List<FileInfo> alterScripts = getAlterScripts();

			// Create databases
			runSqlWithoutTransaction(CREATE_TEST_DATABASES);

			tryWaitingUntilDbReady(CREATE_OLD_TABLES);

			// Run alter scripts, in order
			foreach (FileInfo file in alterScripts)
			{
				ExecuteScriptFile(file.FullName, "Zebra_AlterScript_ScriptRun");
			}

			// Create tables using current version of table script
			ExecuteScriptFile(ConfigurationManager.AppSettings["TrunkPath"] + TABLE_SCRIPT_FILE, "Zebra_AlterScript_CleanTable");

			// Execute the table script twice, to make sure it cleans up after itself properly
			ExecuteScriptFile(ConfigurationManager.AppSettings["TrunkPath"] + TABLE_SCRIPT_FILE, "Zebra_AlterScript_CleanTable");

			// The database target for these scripts does not matter, because they contain the full database references in their select statements.

			// Find any columns that are present in one database, but not in the other
			runSqlWithoutTransaction(CHECK_MISSING_COLUMNS);

			// Find any differences in the column definitions in the two databases (e.g. identity columns, nullable, etc
			runSqlWithoutTransaction(CHECK_TABLE_DIFFERENCES);

		}

		private void tryWaitingUntilDbReady(string sql)
		{
			// We try and block here (for a limited period of time) because though the databases are created in the right order,
			// it seems they may not be avilable to use for some period of time. There is some sort of race condition,
			// trying to work around it by waiting until errors no longer occur.
			DateTime loopStart = UtcDateTime.Now;
			while (true)
			{
				try
				{
					// Create old version of tables for running alter scripts against
					runSqlWithoutTransaction(sql);
				}
				catch (SqlException ex)
				{
					if (ex.Message.Contains("Could not find database ID "))
					{
						Console.WriteLine("Problem reading the database. waiting to retry... ");
						Console.WriteLine(ex.Message);

						// Only keep retrying for 5 seconds
						if (loopStart.AddSeconds(5) < UtcDateTime.Now)
						{
							throw;
						}

						// Wait for a few milliseconds before retrying
						Thread.Sleep(200);
						continue;
					}
					else
					{
						throw;
					}
				}
				break;
			}
		}

		private void runSqlWithoutTransaction (string sql)
		{
			using (SqlConnection conn = new SqlConnection(GetAdoConnectionString()))
			{
				using (SqlCommand command = new SqlCommand(sql))
				{
					conn.Open();

					command.Connection = conn;
					// Wait 30 seconds at most for command to finish. This should be a reasonable number for a unit test.
					command.CommandTimeout = 30;

					command.ExecuteNonQuery();
				}
			}
		}

		public static string ExecuteScriptFile(string file, string databaseName)
		{
			using (Process p = new Process())
			{
				p.StartInfo.UseShellExecute = false;
				p.StartInfo.FileName = "sqlcmd";
				p.StartInfo.Arguments = " -U sa -P Tr@cti0n -l 32 -S tra-nextapp.traction.local\\NEXtGEN -b -I -d " + databaseName + " -i \"" + file + "\"";
				p.StartInfo.CreateNoWindow = true;
				p.StartInfo.RedirectStandardOutput = true;

				p.Start();
				string result = p.StandardOutput.ReadToEnd();
				p.WaitForExit();
				if (p.ExitCode != 0)
				{
					// If there is an error exit code, 
					throw new Exception("Error encountered while running file " + file + " on database " + databaseName + "." +
											Environment.NewLine + "Output was:" + Environment.NewLine + result);
				}
				return result;
			}
		}

		// Find all of the alter script files in the alter script directory, and sort them by version number.
		private List<FileInfo> getAlterScripts()
		{
			DirectoryInfo alterScriptRoot = new DirectoryInfo(ConfigurationManager.AppSettings["TrunkPath"] + ALTER_SCRIPT_DIRECTORY);

			FileInfo[] unsortedAlterScripts = alterScriptRoot.GetFiles();

			List<FileInfo> sortedAlterScripts = new List<FileInfo>(unsortedAlterScripts);
			sortedAlterScripts.Sort(new AlterScriptComparer());

			// Get the first alter script we want to include
			for (int i=0; i < sortedAlterScripts.Count; i++)
			{
				if (sortedAlterScripts[i].Name == FIRST_ALTER_SCRIPT)
				{
					sortedAlterScripts.RemoveRange(0, i);
					break;
				}
			}

			if (sortedAlterScripts.Count == 0)
			{
				throw new Exception("No alter scripts to run!");
			}

			foreach (FileInfo file in sortedAlterScripts)
			{
				Console.WriteLine("Including alter script "+ file.Name);
			}

			return sortedAlterScripts;
		}

		// Expected format of alter script file names is:
		// w.x.y.z.sql
		// where w,x,y,z are all positive integers
		private class AlterScriptComparer : IComparer<FileInfo>
		{
			public int Compare(FileInfo x, FileInfo y)
			{
				// Remove the extension from the file
				int[] xVersionParts = breakIntoParts(x.Name.Substring(0, x.Name.Length - x.Extension.Length));
				int[] yVersionParts = breakIntoParts(y.Name.Substring(0, y.Name.Length - y.Extension.Length));

				for (int i=0; i < xVersionParts.Length; i++)
				{
					if (xVersionParts[i] != yVersionParts[i])
					{
						return xVersionParts[i] - yVersionParts[i];
					}
				}

				// If none of the version numbers, in order, are different... the two files have identical names. That shouldn't be possible...?
				throw new Exception("Two files seem to have identical versions, and therefore identical names? That shouldn't be possible.");
			}

			
			private int[] breakIntoParts (string name)
			{
				string[] parts = name.Split(new char[]{'.'}, StringSplitOptions.RemoveEmptyEntries);

				if (parts.Length != 4)
				{
					throw new FormatException("Script filename does not match a valid version string. There are not four parts separated by full stops.");
				}

				int[] numericParts = new int[4];

				for (int i=0; i < 4; i++)
				{
					try
					{
						numericParts[i] = Convert.ToInt32(parts[i]);
					}
					catch (FormatException fe)
					{
						throw new FormatException ("Script filename does not match a valid campaign master version string. Non numeric characters in version number.", fe);
					}
				}

				return numericParts;
			}
		}

#region Script to drop and recreate test databases

private const string CREATE_TEST_DATABASES = @"

USE [master]

SET IMPLICIT_TRANSACTIONS OFF

IF  EXISTS (SELECT name FROM sys.databases WHERE name = N'Zebra_AlterScript_ScriptRun')
DROP DATABASE [Zebra_AlterScript_ScriptRun]


CREATE DATABASE [Zebra_AlterScript_ScriptRun] ON  PRIMARY 
( NAME = N'Zebra_AlterScript_ScriptRun', FILENAME = N'D:\Program Files\Microsoft SQL Server\MSSQL11.NEXTGEN\MSSQL\DATA\Zebra_AlterScript_ScriptRun.mdf')
 LOG ON 
( NAME = N'Zebra_AlterScript_ScriptRun_log', FILENAME = N'D:\Program Files\Microsoft SQL Server\MSSQL11.NEXTGEN\MSSQL\DATA\Zebra_AlterScript_ScriptRun_log.ldf' , SIZE = 1024KB , FILEGROWTH = 10%)



IF  EXISTS (SELECT name FROM sys.databases WHERE name = N'Zebra_AlterScript_CleanTable')
DROP DATABASE [Zebra_AlterScript_CleanTable]


CREATE DATABASE [Zebra_AlterScript_CleanTable] ON  PRIMARY 
( NAME = N'Zebra_AlterScript_CleanTable', FILENAME = N'D:\Program Files\Microsoft SQL Server\MSSQL11.NEXTGEN\MSSQL\DATA\Zebra_AlterScript_CleanTable.mdf')
 LOG ON 
( NAME = N'Zebra_AlterScript_CleanTable_log', FILENAME = N'D:\Program Files\Microsoft SQL Server\MSSQL11.NEXTGEN\MSSQL\DATA\Zebra_AlterScript_CleanTable_log.ldf' , SIZE = 1024KB , FILEGROWTH = 10%)

SET IMPLICIT_TRANSACTIONS ON

";

#endregion

#region Script to create the old version of the tables, against which the alter scripts will be run

// This is the tables script from revision 1479, the version before the 0.0.0.25 alter script was added.
// Some cleanup is necessary to make this work through ADO, but the table structure has not changed.
// Critical to include the using statement at the start of this script, or your test database will be erased.
private const string CREATE_OLD_TABLES = @"

USE Zebra_AlterScript_ScriptRun



ALTER DATABASE CURRENT
SET READ_COMMITTED_SNAPSHOT ON

IF OBJECT_ID(N'[dbo].[TDX_Form]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Form];

IF OBJECT_ID(N'[dbo].[TDX_FormStyleTemplate]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FormStyleTemplate];

IF OBJECT_ID(N'[dbo].[TDX_Contact]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Contact];

IF OBJECT_ID(N'[dbo].[TDX_ContactField]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField];

IF OBJECT_ID(N'[dbo].[TDX_ContactField_Text]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField_Text];

IF OBJECT_ID(N'[dbo].[TDX_ContactField_Location]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField_Location];

IF OBJECT_ID(N'[dbo].[TDX_ContactField_Number]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField_Number];

IF OBJECT_ID(N'[dbo].[TDX_ContactField_DateTime]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField_DateTime];

IF OBJECT_ID(N'[dbo].[TDX_ContactField_Boolean]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField_Boolean];

IF OBJECT_ID(N'[dbo].[TDX_ContactField_Identifier]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactField_Identifier];

IF OBJECT_ID(N'[dbo].[TDX_FormSubmit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FormSubmit];

IF OBJECT_ID(N'[dbo].[TDX_ContactFieldValue_Text]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactFieldValue_Text];

IF OBJECT_ID(N'[dbo].[TDX_Account]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Account];

IF OBJECT_ID(N'[dbo].[TDX_User]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_User];

IF OBJECT_ID(N'[dbo].[TDX_UserInstance]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UserInstance];

IF OBJECT_ID(N'[dbo].[TDX_Criteria]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Criteria];

IF OBJECT_ID(N'[dbo].[TDX_Criteria_MetaPredicate]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Criteria_MetaPredicate];

IF OBJECT_ID(N'[dbo].[TDX_Criteria_Predicate]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Criteria_Predicate];

IF OBJECT_ID(N'[dbo].[TDX_EmailDesign]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailDesign];

IF OBJECT_ID(N'[dbo].[TDX_EmailDesignVariant]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailDesignVariant];

IF OBJECT_ID(N'[dbo].[TDX_EmailDesign_Template_ContentBlock]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailDesign_Template_ContentBlock];

IF OBJECT_ID(N'[dbo].[TDX_EmailDesignContentBlock]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailDesignContentBlock];

IF OBJECT_ID(N'[dbo].[TDX_EmailDesignTemplate]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailDesignTemplate];

IF OBJECT_ID(N'[dbo].[TDX_DisplayCriteria]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_DisplayCriteria];

IF OBJECT_ID(N'[dbo].[TDX_DisplayCriteriaSegment]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_DisplayCriteriaSegment];

IF OBJECT_ID(N'[dbo].[TDX_DisplayCriteriaCondition]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_DisplayCriteriaCondition];

IF OBJECT_ID(N'[dbo].[TDX_DisplayCriteriaCondition_Parameter]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_DisplayCriteriaCondition_Parameter];

IF OBJECT_ID(N'[dbo].[TDX_ContactList]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactList];

IF OBJECT_ID(N'[dbo].[TDX_ContactListAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactListAudit];

-- table deleted. Remove this declaration after all dev databases have been rebuilt.
IF OBJECT_ID(N'[dbo].[TDX_ContactListMember]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactListMember];

IF OBJECT_ID(N'[dbo].[TDX_ContactListMember]', 'V') IS NOT NULL
    DROP VIEW [dbo].[TDX_ContactListMember];

IF OBJECT_ID(N'[dbo].[TDX_Campaign]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Campaign];

IF OBJECT_ID(N'[dbo].[TDX_CampaignSchedule]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CampaignSchedule];

IF OBJECT_ID(N'[dbo].[TDX_Segment]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Segment];

IF OBJECT_ID(N'[dbo].[TDX_EmailSendQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailSendQueue];

IF OBJECT_ID(N'[dbo].[TDX_EmailSend]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailSend];

IF OBJECT_ID(N'[dbo].[TDX_UserAgent]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UserAgent];

IF OBJECT_ID(N'[dbo].[TDX_Domain]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Domain];

IF OBJECT_ID(N'[dbo].[TDX_EmailOpen]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailOpen];

IF OBJECT_ID(N'[dbo].[TDX_EmailLink]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailLink];

IF OBJECT_ID(N'[dbo].[TDX_EmailLinkClick]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailLinkClick];

IF OBJECT_ID(N'[dbo].[TDX_SMSAggregator]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSAggregator];

IF OBJECT_ID(N'[dbo].[TDX_Configuration]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Configuration];

IF OBJECT_ID(N'[dbo].[TDX_SMSAggregatorConfig]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSAggregatorConfig];

IF OBJECT_ID(N'[dbo].[TDX_SMSMessage]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSMessage];

IF OBJECT_ID(N'[dbo].[TDX_SMSResult]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSResult];

IF OBJECT_ID(N'[dbo].[TDX_SMSDesign]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSDesign];

IF OBJECT_ID(N'[dbo].[TDX_SMSProcessingQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSProcessingQueue];

IF OBJECT_ID(N'[dbo].[TDX_SMSResultPart]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSResultPart];

IF OBJECT_ID(N'[dbo].[TDX_SMSIncomingMessage]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSIncomingMessage];

IF OBJECT_ID(N'[dbo].[TDX_EventQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EventQueue];

IF OBJECT_ID(N'[dbo].[TDX_EventConfiguration]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EventConfiguration];

IF OBJECT_ID(N'[dbo].[TDX_TriggeredSend]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_TriggeredSend];

IF OBJECT_ID(N'[dbo].[TDX_EventBehaviourAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EventBehaviourAudit];

IF OBJECT_ID(N'[dbo].[TDX_RecurringSchedule]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_RecurringSchedule];

IF OBJECT_ID(N'[dbo].[TDX_AutoResponder]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutoResponder];

IF OBJECT_ID(N'[dbo].[TDX_LandingPage]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LandingPage];

IF OBJECT_ID(N'[dbo].[TDX_Role]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Role];

-- keeping this for deleting old table if exists
IF OBJECT_ID(N'[dbo].[TDX_UserRole]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UserRole];

IF OBJECT_ID(N'[dbo].[TDX_UserInstanceRole]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UserInstanceRole];

IF OBJECT_ID(N'[dbo].[TDX_Permission]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Permission];

IF OBJECT_ID(N'[dbo].[TDX_Action]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Action];

IF OBJECT_ID(N'[dbo].[TDX_RolePermissionAction]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_RolePermissionAction];


IF OBJECT_ID(N'[dbo].[TDX_Bounce]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Bounce];


IF OBJECT_ID(N'[dbo].[TDX_FormField]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FormField];


IF OBJECT_ID(N'[dbo].[TDX_EmailSendFailure]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailSendFailure];


IF OBJECT_ID(N'[dbo].[TDX_SMSCampaignProcessingFailure]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSCampaignProcessingFailure];


IF OBJECT_ID(N'[dbo].[TDX_SMSCampaignProcessed]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSCampaignProcessed];


IF OBJECT_ID(N'[dbo].[TDX_EmailBlockList]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailBlockList];


-- table deleted. Remove this declaration after all dev databases have been rebuilt.
IF OBJECT_ID(N'[dbo].[TDX_ContactSubscriptionAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactSubscriptionAudit];


-- table deleted. Remove this declaration after all dev databases have been rebuilt.
IF OBJECT_ID(N'[dbo].[TDX_ContactSubscriptionMember]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactSubscriptionMember];


-- table deleted. Remove this declaration after all dev databases have been rebuilt.
IF OBJECT_ID(N'[dbo].[TDX_ContactSubscription]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactSubscription];


IF OBJECT_ID(N'[dbo].[TDX_APIUser]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_APIUser];


IF OBJECT_ID(N'[dbo].[TDX_APICallAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_APICallAudit];


IF OBJECT_ID(N'[dbo].[TDX_CampaignAnalytics]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CampaignAnalytics];


IF OBJECT_ID(N'[dbo].[TDX_ImportContactSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_ImportContactSetting;


IF OBJECT_ID(N'[dbo].[TDX_ImportContactQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_ImportContactQueue;


IF OBJECT_ID(N'[dbo].[TDX_ContactImportResult]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_ContactImportResult;


IF OBJECT_ID(N'[dbo].[TDX_EmailUnsubscribe]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_EmailUnsubscribe;


IF OBJECT_ID(N'[dbo].[TDX_Tag]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_Tag;


IF OBJECT_ID(N'[dbo].[TDX_TagMember]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_TagMember;


IF OBJECT_ID(N'[dbo].[TDX_AccountSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_AccountSetting;


IF OBJECT_ID(N'[dbo].[TDX_EmailExternalContent]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_EmailExternalContent;


IF OBJECT_ID(N'[dbo].[TDX_ExternalContent]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_ExternalContent;


IF OBJECT_ID(N'[dbo].[TDX_Resource]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Resource];


IF OBJECT_ID(N'[dbo].[TDX_ScheduledReport]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ScheduledReport];


IF OBJECT_ID(N'[dbo].[TDX_CampaignLitmusAnalytics]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CampaignLitmusAnalytics];


IF OBJECT_ID(N'[dbo].[TDX_SystemContent]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SystemContent];


IF OBJECT_ID(N'[dbo].[TDX_SubDomain]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SubDomain];


IF OBJECT_ID(N'[dbo].[TDX_LoginAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LoginAudit];


IF OBJECT_ID(N'[dbo].[TDX_ContactSubscriptionAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactSubscriptionAudit];

IF OBJECT_ID(N'[dbo].[TDX_EmailAlias]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_EmailAlias];

IF OBJECT_ID(N'[dbo].[TDX_Criteria_Expression_Parameter]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Criteria_Expression_Parameter];

IF OBJECT_ID(N'[dbo].[TDX_Criteria_Expression]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Criteria_Expression];

IF OBJECT_ID(N'[dbo].[TDX_ContactMeasure_Parameter]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactMeasure_Parameter];

IF OBJECT_ID(N'[dbo].[TDX_ContactMeasure]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactMeasure];

IF OBJECT_ID(N'[dbo].[TDX_MeasureDefinition_Parameter]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_MeasureDefinition_Parameter];

IF OBJECT_ID(N'[dbo].[TDX_MeasureDefinition]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_MeasureDefinition];

IF OBJECT_ID(N'[dbo].[TDX_SMSBlockList]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SMSBlockList];

IF OBJECT_ID(N'[dbo].[TDX_ABTestResult]', 'U') IS NOT NULL
    DROP TABLE [dbo].Tdx_ABTestResult;

IF OBJECT_ID(N'[dbo].[TDX_SMSUnsubscribe]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_SMSUnsubscribe;

IF OBJECT_ID(N'[dbo].[TDX_ApiEvent]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ApiEvent];

IF TYPE_ID(N'TDX_TYPE_ApiEventType') IS NOT NULL
	DROP TYPE [dbo].[TDX_TYPE_ApiEventType]

IF OBJECT_ID(N'[dbo].[TDX_FileTransferSchedule]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FileTransferSchedule];


IF OBJECT_ID(N'[dbo].[View_TDX_CustomInteraction_NameCache]', 'V') IS NOT NULL
    DROP VIEW [dbo].[View_TDX_CustomInteraction_NameCache];


IF OBJECT_ID(N'[dbo].[TDX_CustomInteraction]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomInteraction];


IF OBJECT_ID(N'[dbo].[TDX_ImportCustomInteractionQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ImportCustomInteractionQueue];


IF OBJECT_ID(N'[dbo].[TDX_ImportCustomInteractionSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ImportCustomInteractionSetting];


IF OBJECT_ID(N'[dbo].[TDX_CustomInteractionImportResult]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomInteractionImportResult];


IF OBJECT_ID(N'[dbo].[TDX_CampaignApprover]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CampaignApprover];


IF OBJECT_ID(N'[dbo].[TDX_CampaignApprovalAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CampaignApprovalAudit];


IF OBJECT_ID(N'[dbo].[TDX_AccountInternalSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AccountInternalSetting];


IF OBJECT_ID(N'[dbo].[TDX_CustomReport]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomReport];


IF OBJECT_ID(N'[dbo].[TDX_CustomReportAccount]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomReportAccount];


IF OBJECT_ID(N'[dbo].[TDX_Automation]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Automation];


IF OBJECT_ID(N'[dbo].[TDX_AutomationListenerWait]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationListenerWait];


IF OBJECT_ID(N'[dbo].[TDX_AutomationTriggerEmailOpen]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationTriggerEmailOpen];


IF OBJECT_ID(N'[dbo].[TDX_AutomationListenerEmailOpen]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationListenerEmailOpen];


IF OBJECT_ID(N'[dbo].[TDX_AutomationTriggerEmailClick]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationTriggerEmailClick];


IF OBJECT_ID(N'[dbo].[TDX_AutomationListenerEmailClick]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationListenerEmailClick];


IF OBJECT_ID(N'[dbo].[TDX_AutomationTriggerFormSubmit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationTriggerFormSubmit];


IF OBJECT_ID(N'[dbo].[TDX_AutomationListenerFormSubmit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationListenerFormSubmit];


IF OBJECT_ID(N'[dbo].[TDX_AutomationWorkItemAudit]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationWorkItemAudit];


IF OBJECT_ID(N'[dbo].[TDX_AutomationWorkItem]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationWorkItem];


IF OBJECT_ID(N'[dbo].[TDX_AutomationEventQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationEventQueue];


IF OBJECT_ID(N'[dbo].[TDX_AutomationEventQueueHistory]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationEventQueueHistory];


IF OBJECT_ID(N'[dbo].[TDX_CustomReportInstance]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomReportInstance];


IF OBJECT_ID(N'[dbo].[TDX_CustomReportResult]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomReportResult];


IF OBJECT_ID(N'[dbo].[TDX_AutomationHistory]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationHistory];


IF OBJECT_ID(N'[dbo].[TDX_AutomationStatusHistory]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_AutomationStatusHistory];


IF OBJECT_ID(N'[dbo].[TDX_RoiTrackEvent]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_RoiTrackEvent];


IF OBJECT_ID(N'[dbo].[TDX_FileProfile]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FileProfile];


IF OBJECT_ID(N'[dbo].[TDX_CustomReportParameter]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomReportParameter];


IF OBJECT_ID(N'[dbo].[TDX_CustomReportParameterValue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomReportParameterValue];


IF OBJECT_ID(N'[dbo].[TDX_ScheduledReportInstance]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ScheduledReportInstance];


IF OBJECT_ID(N'[dbo].[TDX_FeedbackLoop]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FeedbackLoop];


IF OBJECT_ID(N'[dbo].[TDX_UserGroup]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UserGroup];


IF OBJECT_ID(N'[dbo].[TDX_UserGroupRole]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UserGroupRole];


IF OBJECT_ID(N'[dbo].[TDX_DashboardAccountStatistic]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_DashboardAccountStatistic];


IF OBJECT_ID(N'[dbo].[TDX_ContactFieldDeleteQueue]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ContactFieldDeleteQueue];


IF OBJECT_ID(N'[dbo].[TDX_SMSMessageLookup]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_SMSMessageLookup;


IF OBJECT_ID(N'[dbo].[TDX_CampaignStatistic]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_CampaignStatistic;


IF OBJECT_ID(N'[dbo].[TDX_GoogleAnalyticReportSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_GoogleAnalyticReportSetting;


IF OBJECT_ID(N'[dbo].[TDX_GoogleAnalyticReportSetting_Metric]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_GoogleAnalyticReportSetting_Metric;


/* Swipe n Go tables */

IF OBJECT_ID(N'[dbo].[TDX_Payment]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Payment];

IF OBJECT_ID(N'[dbo].[TDX_PreSignup]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_PreSignup];

IF OBJECT_ID(N'[dbo].[TDX_SubscriptionLine]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SubscriptionLine];

IF OBJECT_ID(N'[dbo].[TDX_Subscription]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Subscription];

IF OBJECT_ID(N'[dbo].[TDX_InvoiceLine]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_InvoiceLine];

IF OBJECT_ID(N'[dbo].[TDX_Invoice]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Invoice];

IF OBJECT_ID(N'[dbo].[TDX_OrderLine]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_OrderLine];

IF OBJECT_ID(N'[dbo].[TDX_Order]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Order];

IF OBJECT_ID(N'[dbo].[TDX_PlanLine]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_PlanLine;

IF OBJECT_ID(N'[dbo].[TDX_Plan]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Plan];

IF OBJECT_ID(N'[dbo].[TDX_DiscountType]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_DiscountType];

IF OBJECT_ID(N'[dbo].[TDX_Discount]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Discount];

IF OBJECT_ID(N'[dbo].[TDX_Service]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Service];

IF OBJECT_ID(N'[dbo].[TDX_APICallLimitSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_APICallLimitSetting];


IF OBJECT_ID(N'[dbo].[TDX_UsageHistory]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_UsageHistory];

IF OBJECT_ID(N'[dbo].[TDX_PaymentSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_PaymentSetting];

IF OBJECT_ID(N'[dbo].[TDX_Currency]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Currency];

IF OBJECT_ID(N'[dbo].[TDX_Price]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Price];

IF OBJECT_ID(N'[dbo].[TDX_CustomService]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CustomService];

IF OBJECT_ID(N'[dbo].[TDX_Notification]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_Notification];

IF OBJECT_ID(N'[dbo].[TDX_CostPerIndex]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_CostPerIndex];

IF OBJECT_ID(N'[dbo].[TDX_SubscriptionUpdate]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SubscriptionUpdate];

IF OBJECT_ID(N'[dbo].[TDX_PlanDefinition]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_PlanDefinition];

IF OBJECT_ID(N'[dbo].[TDX_ReportAccountCoding]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_ReportAccountCoding];

IF OBJECT_ID(N'[dbo].[TDX_SpecialOfferLink]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_SpecialOfferLink];


IF OBJECT_ID(N'[dbo].[TDX_FacebookAccountSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FacebookAccountSetting];

IF OBJECT_ID(N'[dbo].[TDX_FacebookPageSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FacebookPageSetting];

IF OBJECT_ID(N'[dbo].[TDX_FacebookAutoPostSetting]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_FacebookAutoPostSetting];

IF OBJECT_ID(N'[dbo].[TDX_FacebookAutoPostPage]', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_FacebookAutoPostPage;


/* Swipe n go tables end*/

/* Lead Score tables start */

IF OBJECT_ID(N'[dbo].[TDX_LeadScoreConfig]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreConfig];


IF OBJECT_ID(N'[dbo].[TDX_LeadScore]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScore];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreAction]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreAction];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreActionOverride]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreActionOverride];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreContact]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreContact];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreEventProcessTracker]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreEventProcessTracker];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreEvent]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreEvent];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreInteraction]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreInteraction];


IF OBJECT_ID(N'[dbo].[TDX_LeadScoreThreshold]', 'U') IS NOT NULL
    DROP TABLE [dbo].[TDX_LeadScoreThreshold];


/* Lead Score tables end */

/* ECommerce Tables Start */

IF OBJECT_ID(N'[dbo].[TDX_ECommerceAccountSetting]', 'U') IS NOT NULL
	DROP TABLE [TDX_ECommerceAccountSetting]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceAccountConfig]', 'U') IS NOT NULL
	DROP TABLE [TDX_ECommerceAccountConfig]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceEntity]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceEntity]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceAccount]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceAccount]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceStoreProduct]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceStoreProduct]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceStoreProductVariant]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceStoreProductVariant]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceStoreOrder]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceStoreOrder]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceStoreOrderItem]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceStoreOrderItem]

IF OBJECT_ID(N'[dbo].[TDX_ECommerceProdRecResult]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceProdRecResult]


IF OBJECT_ID(N'[dbo].[TDX_ECommerceProdRecModel]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceProdRecModel]


IF OBJECT_ID(N'[dbo].[TDX_ECommerceProdRecJobContacts]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceProdRecJobContacts]


IF OBJECT_ID(N'[dbo].[TDX_ECommerceProdRecJob]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceProdRecJob]


IF OBJECT_ID(N'[dbo].[TDX_ECommerceProdRecJobRun]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ECommerceProdRecJobRun]


/* ECommerce Tables End */

IF OBJECT_ID(N'[dbo].[TDX_CompetitionItem]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_CompetitionItem]


IF OBJECT_ID(N'[dbo].[TDX_CompetitionFormField]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_CompetitionFormField]


IF OBJECT_ID(N'[dbo].[TDX_CompetitionEntry]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_CompetitionEntry]


IF OBJECT_ID(N'[dbo].[TDX_CompetitionAction]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_CompetitionAction]


IF OBJECT_ID(N'[dbo].[TDX_EmailAddress]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_EmailAddress]






/* Mobile Wallet Tables Start */

IF OBJECT_ID(N'[dbo].[TDX_MobileWalletRegistration]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletRegistration]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletS2apRegistration]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletS2apRegistration]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletTransactionLog]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletTransactionLog]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletCertificate]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletCertificate]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletS2apCredential]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletS2apCredential]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletProjectChangeHistory]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletProjectChangeHistory]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletProject]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletProject]


IF OBJECT_ID(N'[dbo].[TDX_MobileWalletPassUpdate]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_MobileWalletPassUpdate]


IF OBJECT_ID(N'[dbo].[TDX_ServiceAccountConfig]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_ServiceAccountConfig]

/* Mobile Wallet Tables End */

/* Form File Upload Table */
IF OBJECT_ID(N'[dbo].[TDX_FormFileUpload]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_FormFileUpload]


IF OBJECT_ID(N'[dbo].[TDX_DomainMonitoringDetails]', 'U') IS NOT NULL
	DROP TABLE [dbo].[TDX_DomainMonitoringDetails]


IF OBJECT_ID(N'[dbo].[TDX_SMSIncomingAction]', 'U') IS NOT NULL
	DROP TABLE [dbo].TDX_SMSIncomingAction


IF OBJECT_ID(N'[dbo].[TDX_SMSIncomingSubscribe]', 'U') IS NOT NULL
	DROP TABLE [dbo].TDX_SMSIncomingSubscribe


IF OBJECT_ID(N'[dbo].[TDX_SMSEndpoint]', 'U') IS NOT NULL
	DROP TABLE [dbo].TDX_SMSEndpoint


IF OBJECT_ID(N'[dbo].[TDX_UserAudit]', 'U') IS NOT NULL
	DROP TABLE [dbo].TDX_UserAudit


IF TYPE_ID(N'TDX_TYPE_EmailDesignId') IS NOT NULL
	DROP TYPE [dbo].[TDX_TYPE_EmailDesignId]


/* GDPR Audit Tables Start */

IF OBJECT_ID(N'[dbo].[TDX_GDPRAudit]', 'U') IS NOT NULL
	DROP TABLE [dbo].TDX_GDPRAudit


/* GDPR Audit Tables End */

IF OBJECT_ID(N'[dbo].TDX_GdprRequest', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_GdprRequest;


IF OBJECT_ID(N'[dbo].TDX_GdprDataProtectionOfficer', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_GdprDataProtectionOfficer;


IF OBJECT_ID(N'[dbo].TDX_CompetitionSMSEntry', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_CompetitionSMSEntry;


IF OBJECT_ID(N'[dbo].TDX_CompetitionSMSField', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_CompetitionSMSField;


IF OBJECT_ID(N'[dbo].TDX_CompetitionKeywordAndContent', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_CompetitionKeywordAndContent;


/* Umbrella specific Tables */

IF OBJECT_ID(N'[dbo].TDX_CustomerServiceTeam', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_CustomerServiceTeam;


IF OBJECT_ID(N'[dbo].TDX_HandlingHouse', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_HandlingHouse;


/* Umbrella specific Tables End */


/* API adapter specific Tables */
IF OBJECT_ID(N'[dbo].TDX_AdapterAudit', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_AdapterAudit;


IF OBJECT_ID(N'[dbo].TDX_AdapterDynamicAPI', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_AdapterDynamicAPI;


IF OBJECT_ID(N'[dbo].TDX_AdapterAPIUserMapping', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_AdapterAPIUserMapping;


IF OBJECT_ID(N'[dbo].TDX_AdapterContactFieldMapping', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_AdapterContactFieldMapping;


IF OBJECT_ID(N'[dbo].TDX_AdapterContactListMapping', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_AdapterContactListMapping;

/* API adapter specific End */

/* Domain Cert Table */
IF OBJECT_ID(N'[dbo].TDX_DomainCert', 'U') IS NOT NULL
    DROP TABLE [dbo].TDX_DomainCert;


-- --------------------------------------------------
-- Creating all tables
-- --------------------------------------------------

CREATE TABLE [dbo].[TDX_Form] (
    [Id] bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
    [AccountId] bigint NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [BuilderData] nvarchar(max) NULL,
    [Content] nvarchar(max) NULL,
    [Enabled] bit NOT NULL,
    [StartDateTime] datetime2 NULL,
    [EndDateTime] datetime2 NULL,
    [RedirectUrl] varchar(255) NULL,
    [Description] nvarchar(512) NULL,
    [SuccessAction] nvarchar(50) NOT NULL,
    [SuccessMessage] nvarchar(255) NULL,
    [EmailNotification] bit NOT NULL,
	[NotificationAddress] nvarchar(512) NULL,
	[Editor] nvarchar(50) NOT NULL,
	[FormGuid] [uniqueidentifier] NOT NULL,
	[PermitInsecure] bit NOT NULL,
	FormType varchar(50) NOT NULL
);



CREATE TABLE [dbo].[TDX_FormStyleTemplate] (
    [Id] bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
    [AccountId] bigint NOT NULL,
    [Name] nvarchar(255) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [Enabled] bit NOT NULL,
    [Created] datetime2 NULL,
    [Modified] datetime2 NULL
);



CREATE TABLE [dbo].[TDX_ContactField] (
    [Id] bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
    [AccountId] bigint NOT NULL,
    [Name] nvarchar(255) NOT NULL,
	[ValidationType] nvarchar(50) NOT NULL,
    [ValidationRules] nvarchar(max) NULL,
    [CanonicalizationRules] nvarchar(max) NULL,
    [FieldType] varchar(50) NOT NULL,
	[PredefinedValues] bit NOT NULL,
	[MaxLength] int NOT NULL,
	[Important] bit NOT NULL,
	[Precision] varchar(50) NULL,
	[Modified] datetime2 NOT NULL
);


CREATE TABLE [dbo].[TDX_ContactFieldValue_Text] (
	[Id] bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
    [FieldId] bigint NOT NULL,
	[Label] nvarchar(255) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
	[Position] int NOT NULL
)


CREATE TABLE [dbo].[TDX_FormSubmit] (
    [Id] bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
    [FormId] bigint NOT NULL,
    [ContactId] bigint NULL,
	[SubmissionAt] datetime2 NOT NULL,
	[SubmittedData] nvarchar(max) NOT NULL,
	[IPAddress] varbinary(16) NOT NULL,
	[UserAgentId] bigint NOT NULL
)



create table TDX_Account (
	[Id] bigint PRIMARY KEY NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[ParentId] bigint NOT NULL,
	[Active] bit NOT NULL,
	[TimeZoneId] nvarchar(100) NOT NULL,
	[CryptoKey] varbinary(32) NOT NULL,
	[ContactStorage] varchar(10) NOT NULL,
	[IsEnterprise] bit NOT NULL,
	[ExpiryDate] datetime2 NULL,
	[Is2FARequired] bit NOT NULL,
	[AccountGuid] [uniqueidentifier] NOT NULL
)

ALTER TABLE [dbo].[TDX_Account] ADD CONSTRAINT DF_DEFAULT_TDX_Account_Is2FARequired DEFAULT 0 FOR [Is2FARequired]
ALTER TABLE [dbo].[TDX_Account] ADD CONSTRAINT DF_DEFAULT_TDX_Account_IsEnterprise DEFAULT 0 FOR [IsEnterprise]




create table TDX_User (
	[Id] bigint PRIMARY KEY IDENTITY,
	[Name] nvarchar(255) NOT NULL,
	[Username] nvarchar(255) NOT NULL,
	[EmailAddress] nvarchar(255) NOT NULL,
	[Salt] nvarchar(255) NOT NULL,
	[Password] nvarchar(255) NOT NULL,
	[MobileNumber] nvarchar(255) NOT NULL,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL,
	[LoginAttempts] int NOT NULL,
	[SuperUser] bit NOT NULL,
	[HomeAccount] bigint NOT NULL,
	[ShowUserGuide] bit NOT NULL,
	[PasswordLastUpdated] datetime2 NOT NULL,
	[LastLoginTime] datetime2 NOT NULL,
	[Is2FAEnabled] bit  NOT NULL,
	[TOTPSharedKey] varchar(50),
	[LastLoginTime2FA] datetime2,
	[TFATypeId] int NOT NULL
	CONSTRAINT uc_tdx_user_EmailAddress UNIQUE (EmailAddress)
)

ALTER TABLE [dbo].[TDX_User] ADD CONSTRAINT DF_DEFAULT_TDX_User_Is2FAEnabled DEFAULT 0 FOR [Is2FAEnabled]




create table TDX_UserInstance (
	[Id] bigint PRIMARY KEY IDENTITY,
	[AccountId] bigint NOT NULL,
	[UserId] bigint NOT NULL,
	[Active] bit NOT NULL,
	[Admin] bit NOT NULL,
	[UserGroupId] bigint,
	[BillingAdmin] bit NOT NULL,
	[ReceiveSystemNotification] bit,
	[ReceiveGeneralNotification] bit
)



ALTER TABLE [dbo].[TDX_UserInstance] ADD CONSTRAINT DF_DEFAULT_TDX_UserInstance_ReceiveSystemNotification DEFAULT 0 FOR [ReceiveSystemNotification]
ALTER TABLE [dbo].[TDX_UserInstance] ADD CONSTRAINT DF_DEFAULT_TDX_UserInstance_ReceiveGeneralNotification DEFAULT 0 FOR [ReceiveGeneralNotification]


create table TDX_Criteria (
	Id bigint PRIMARY KEY IDENTITY,
	AccountId bigint NOT NULL,
	DisplayCriteriaId bigint NULL,
	LastCount bigint NULL,
	CachedValidCount bigint NULL,
	CachedBlockCount bigint NULL,
	CachedEmptyCount bigint NULL,
	CachedUnsubscribeCount bigint NULL
)



create table TDX_Criteria_MetaPredicate (
	CriteriaId bigint NOT NULL,
	PredicateIndex bigint NOT NULL,
	ParentIndex bigint NOT NULL,
	Type varchar(50) NOT NULL,
	-- Using negate instead of Not, because Not is a reserved keyword.
	Negate bit NULL,
	BooleanOperator varchar(50) NULL,
	AggregateFunction varchar(50) NULL
)



ALTER TABLE TDX_Criteria_MetaPredicate
ADD CONSTRAINT PK_TDX_Criteria_MetaPredicate
    PRIMARY KEY CLUSTERED (
	CriteriaId,
	PredicateIndex
)



create table TDX_Criteria_Predicate (
	CriteriaId bigint NOT NULL,
	PredicateIndex bigint NOT NULL,
	ParentIndex bigint NOT NULL,
	Type varchar(50) NOT NULL,
	Operator varchar(50) NULL
)



ALTER TABLE TDX_Criteria_Predicate
ADD CONSTRAINT PK_TDX_Criteria_Predicate
    PRIMARY KEY CLUSTERED (
	CriteriaId,
	PredicateIndex
)



create table TDX_Criteria_Expression (
	CriteriaId bigint NOT NULL,
	ExpressionIndex bigint NOT NULL,
	PredicateParentIndex bigint NOT NULL,
	Type nvarchar(50) NOT NULL,
	TableName nvarchar(255) NULL,
	ColumnName nvarchar(255) NULL,
	ContactFieldId bigint NULL,
	ContactFieldType nvarchar(255) NULL,
	StringExpression nvarchar(4000) NULL,
	NumberExpression numeric(28,5) NULL,
	DateExpression datetime2 NULL,
	BooleanExpression bit NULL,
	SystemFieldName nvarchar(50) NULL,
	SqlExpression nvarchar(max) NULL,
	SegmentId bigint NULL,
	ListId bigint NULL,
	DateRangeOffset int NULL,
	DateRangeLength int NULL,
	DateRangeType nvarchar(50) NULL,
	ContactMeasureId bigint NULL,
	MeasureDefinitionId bigint NULL,
	DateRangeOffsetDatePart nvarchar(50) NULL,
	DateRangeLengthDatePart nvarchar(50) NULL,
	LeadScoreId bigint NULL
)



ALTER TABLE TDX_Criteria_Expression
ADD CONSTRAINT PK_TDX_Criteria_Expression
    PRIMARY KEY CLUSTERED (
	CriteriaId,
	ExpressionIndex
)



create table TDX_Criteria_Expression_Parameter (
	CriteriaId bigint NOT NULL,
	ExpressionIndex bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	StringValue nvarchar(255) NULL,
	NumberValue numeric(28,5) NULL,
	DateValue datetime2 NULL,
	BooleanValue bit NULL	
)



ALTER TABLE TDX_Criteria_Expression_Parameter
ADD CONSTRAINT PK_TDX_Criteria_Expression_Parameter
    PRIMARY KEY CLUSTERED (
	CriteriaId,
	ExpressionIndex,
	Name
)



create table TDX_EmailDesign (
	Id bigint NOT NULL PRIMARY KEY IDENTITY(1,1),
	Name nvarchar(255) NOT NULL,
	AccountId bigint NOT NULL,
	Editor nvarchar(50) NOT NULL
)



create table TDX_EmailDesignVariant (
	Id bigint NOT NULL PRIMARY KEY IDENTITY(1,1),
	DesignId bigint NOT NULL,
	VariantLabel nvarchar(255) NOT NULL,
	FromName nvarchar(255) NOT NULL,
	FromAddress nvarchar(255) NOT NULL,
	[Subject] nvarchar(255) NOT NULL,
	HtmlEncoding nvarchar(50) NOT NULL,
	HtmlContent nvarchar(max) NOT NULL,
	TextEncoding nvarchar(50) NOT NULL,
	TextContent nvarchar(max) NOT NULL,
	Tested datetime2 NULL,
	ToAddressContactFieldId bigint NOT NULL,
	ReplyTo NVARCHAR(255) NULL,
	[PreHeader] nvarchar(255) NULL,
	Modified datetime2 NOT NULL,
	[Cc] [nvarchar](255) NULL, 
	[Bcc] [nvarchar](255) NULL,
	PublicId UniqueIdentifier NOT NULL
)




create table TDX_EmailDesignContentBlock (
	Id bigint NOT NULL PRIMARY KEY IDENTITY(1,1),
	AccountId bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	Tag nvarchar(255) NULL,
	Content nvarchar(max) NOT NULL
)



create table TDX_EmailDesignTemplate (
	Id bigint NOT NULL PRIMARY KEY IDENTITY(1,1),
	AccountId bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	Tag nvarchar(255) NULL,
	Content nvarchar(max) NOT NULL
)



create table TDX_EmailDesign_Template_ContentBlock (
	ContentBlockId bigint NOT NULL,
	TemplateId bigint NOT NULL
)



ALTER TABLE TDX_EmailDesign_Template_ContentBlock
ADD CONSTRAINT PK_TDX_EmailDesign_Template_ContentBlock
    PRIMARY KEY CLUSTERED (
	ContentBlockId,
	TemplateId
)



-- Foreign keys to prevent orphans in this table. Not really critical, could remove these if they get annoying.
alter table TDX_EmailDesign_Template_ContentBlock
add constraint FK_TDX_EmailDesign_Template_ContentBlock_ContentBlockId
FOREIGN KEY (ContentBlockId)
references TDX_EmailDesignContentBlock (Id)

alter table TDX_EmailDesign_Template_ContentBlock
add constraint FK_TDX_EmailDesign_Template_ContentBlock_TemplateId
FOREIGN KEY (TemplateId)
references TDX_EmailDesignTemplate (Id)



create table TDX_DisplayCriteria (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	AccountId bigint NOT NULL,
	DisplayType nvarchar(50) NOT NULL,
	SubscriptionId bigint NULL
)



create table TDX_DisplayCriteriaSegment (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	DisplayCriteriaId bigint NOT NULL,
	Position int NOT NULL,
	InternalOperator nvarchar(50) NOT NULL,
	ExternalOperator nvarchar(50) NOT NULL,
)



create table TDX_DisplayCriteriaCondition (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	DisplayCriteriaSegmentId bigint NOT NULL,
	Position int NOT NULL,
	ConditionType nvarchar(255) NOT NULL,
	Operand1Id bigint NULL,
	Operand1String nvarchar(1000) NULL,
	Operator nvarchar(50) NULL,
	Operand2String nvarchar(1000) NULL,
	DateRangeType nvarchar(50) NULL,
	Operand2Length nvarchar(1000) NULL,
	Operand2OffsetDatePart nvarchar(50) NULL,
	Operand2LengthDatePart nvarchar(50) NULL,

	-- Used only for custom interactions. This is ugly
	CustomInteractionNames nvarchar(1000) NULL,
	CustomInteractionDateStart varchar(50) NULL,
	CustomInteractionDateEnd varchar(50) NULL,
	CustomInteractionTextValue nvarchar(1000) NULL,
	CustomInteractionTextValueOperator nvarchar(50) NULL,
	CustomInteractionNumericValue nvarchar(50) NULL,
	CustomInteractionNumericValueOperator nvarchar(50) NULL,
	CustomInteractionAggregateFunction nvarchar(50) NULL,
	CustomInteractionAggregateValue nvarchar(50) NULL,
	CustomInteractionAggregateValueOperator nvarchar(50) NULL,

	-- Used only for roi track event. We may use generic columns for custominteraction and roi tracking
	[ROITrackEventNames] [nvarchar](1000) NULL,
	[ROITrackEventMessageIds] [nvarchar](1000) NULL,
	[ROITrackEventMessageIdsInOperator] [nvarchar](50) NULL,
	[ROITrackEventDateStart] [varchar](50) NULL,
	[ROITrackEventDateEnd] [varchar](50) NULL,
	[ROITrackEventTextValue] [nvarchar](1000) NULL,
	[ROITrackEventTextValueOperator] [nvarchar](50) NULL,
	[ROITrackEventNumericValue] [nvarchar](50) NULL,
	[ROITrackEventNumericValueOperator] [nvarchar](50) NULL,
	[ROITrackEventAggregateFunction] [nvarchar](50) NULL,
	[ROITrackEventAggregateValue] [nvarchar](50) NULL,
	[ROITrackEventAggregateValueOperator] [nvarchar](50) NULL,

	-- for lead score
	LeadScoreSearchType nvarchar(50),
	LeadScoreId nvarchar(50),
	LeadScoreThresholdId nvarchar(50),
	LeadScoreNumericValue nvarchar(50),
	LeadScoreNumericValueOperator nvarchar(50) NULL,

	-- For eCommerce
	[ECommerceOrderPrice] [nvarchar](50) NULL,
    [ECommerceOrderPriceValueOperator] [nvarchar](50) NULL,
    [ECommerceFinancialAuthorization] [nvarchar](50) NULL,
    [ECommerceFulfillmentStatus] [nvarchar](50) NULL,
    [ECommerceOrderConfirmation] [nvarchar](50) NULL,
    [ECommerceProductId] [nvarchar](50) NULL,
    [ECommerceProductPriceValueOperator] [nvarchar](50) NULL,
    [ECommerceProductPrice] [nvarchar](50) NULL,
    [ECommerceProductTitle] [nvarchar](1000) NULL,
    [ECommerceProductType] [nvarchar](1000) NULL,
    [ECommerceProductTag] [nvarchar](1000) NULL,
    [ECommerceProductVendor] [nvarchar](1000) NULL,
    [ECommerceDateStart] [varchar](50) NULL,
    [ECommerceDateEnd] [varchar](50) NULL,

	--For operators requiring more than one operand value eg. between
	Operand3String nvarchar(1000) NULL
)



create table TDX_DisplayCriteriaCondition_Parameter (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	DisplayCriteriaConditionId bigint NOT NULL,
	Name nvarchar(255) NULL,
	Value nvarchar(1000) NULL
)



create table TDX_ContactList(
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	AccountId bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	Description nvarchar(255) NULL,
	Created datetime2 NOT NULL,
	CreatedBy bigint NOT NULL,
	Modified datetime2 NOT NULL,
	ModifiedBy bigint NOT NULL,
	IsDeleted bit NOT NULL,
	IsSubscriptionList bit NOT NULL,
	SubscriptionPurpose nvarchar(1000) NULL
)



-- This table design is a somewhat complicated arrangement, so here is an explanation of why it is so.
-- Firstly, the Id column exists for two reasons. Firstly, we must have a unique column in the table for
-- practical purposes - i.e. maintenance, deletes, etc. Secondly, this provides a guaranteed ordering
-- for inserts into the table, so we do not need to rely on date values for ordering. The Id column will
-- never be used for lookups. For this reason, it is an identity column and a primary key, but it is
-- *not* the clustered index. The clustered index is created below, and is intended to optimize the
-- most important searches on the table - retrieving the list of contacts currently on the list, and
-- finding the most recent row for a list id and a contact id.
create table TDX_ContactListAudit (
	Id bigint IDENTITY NOT NULL,
	ContactId bigint NOT NULL,
	ContactListId bigint NOT NULL,
	Action char(3) NOT NULL, -- enum, 
	Date datetime2 NOT NULL,
	AgentType tinyint NULL,
	UserInstanceId bigint SPARSE NULL,
	ImportContactQueueId bigint SPARSE NULL,
	FormSubmitId bigint SPARSE NULL,
	EmailUnsubscribeId bigint SPARSE NULL,
	ApiUserId bigint SPARSE NULL,
	SMSIncomingActionId BIGINT SPARSE NULL,
	SmsUnsubscribeId bigint SPARSE NULL
)



-- The primary key is not clustered - this is because (at time of writing) we will not be performing
-- lookups on this table using the Id.
ALTER TABLE TDX_ContactListAudit
ADD CONSTRAINT PK_TDX_ContactListAudit
	PRIMARY KEY NONCLUSTERED (
	Id
)



-- This clustered index is designed to optimize lookups on this table for list id and contact id.
-- There are two main cases. Firstly, retrieving all contacts on a list. Secondly, determining if a
-- single contact is on a specific list, not on a specific list, or was previous on the list but has
-- been removed.
-- Hence the ordering of columns in this index:
-- 1) ContactListId: *every* lookup on the table will use this, and we will often want to get every
--					row matching a specific list id. i.e. there will be many entries for each list
--					id and those entries will be retrieved as a block.
-- 2) ContactId: *every* lookup on the table will use this, but the values are sparse - contact id
--					is unique across all clients and thus related numeric values are not contiguous,
--					and therefore similar contact ids are probably unrelated. Sorting on this id
--					will not result in contiguous data on disk.
-- 3) Id: Relevant when loading a whole contact list. The sorting is used to get the highest id
--				which exists for each list id and contact id. As this lookup is dependant on the
--				other two columns, it comes third.
-- Lastly, we do not need to include any other columns that will be returned as data. Because this
-- is a clustered index, all other columns are implicitly included in this index.
CREATE CLUSTERED INDEX IDX_TDX_ContactListAudit ON TDX_ContactListAudit
(
	ContactListId, ContactId, Id
)





create table TDX_Campaign (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	Name nvarchar(255) NOT NULL,
	Description nvarchar(255) NULL,
	AccountId bigint NOT NULL,
	Status nvarchar(50) NOT NULL, -- Draft, Sent, Scheduled, etc
	Type nvarchar(50) NOT NULL, -- Email or SMS
	DesignId bigint NULL, -- This is either EmailDesignId or SmsId, depending on Type
	-- GoogleAnalyticsId - Tricky. Should this be a separate table per type of analytics, or a separate object with only an AnalyticsId referenced here.
	--						Going to leave it out for now. but will have to come back to this.
	CriteriaId bigint NULL,
	ScheduleId bigint NULL,
	Created datetime2 NOT NULL,
	CreatedBy bigint NOT NULL,
	Modified datetime2 NOT NULL,
	ModifiedBy bigint NOT NULL,
	CampaignAnalyticsId bigint NULL,
	CampaignLitmusAnalyticsId bigint NULL,
	CreatedSource NVARCHAR(50) NOT NULL,
	ModifiedSource NVARCHAR(50) NOT NULL,
	RoiTrackEnabled bit NOT NULL,
	RoiTrackName NVARCHAR(255) NULL,
	SMSUnsubscribeType NVARCHAR(50) NULL,
	FacebookAutoPostSettingId BIGINT NULL,
	SubDomainId BIGINT NULL
)



create table TDX_CampaignSchedule (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	ScheduledLaunchDateTime datetime2 NULL,
	StartSendDateTime datetime2 NULL,
	FinishSendDateTime datetime2 NULL,
	ABSendRule nvarchar(50) NOT NULL, -- open rate, click rate, manual
	ABSendHours int NOT NULL,
	ScheduledBy bigint NULL,
	ABPercentage int NOT NULL,
	ScheduledSource NVARCHAR(50) NOT NULL
)



create table TDX_Segment(
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	AccountId bigint NOT NULL,
	Name nvarchar(255) NULL,
	CriteriaId bigint NOT NULL,	
	Created datetime2 NOT NULL,
	CreatedBy bigint NOT NULL,
	Modified datetime2 NOT NULL,
	ModifiedBy bigint NOT NULL,
	IsDeleted bit NOT NULL
)



create table TDX_EmailSendQueue (
	Id bigint NOT NULL,

	-- About the email to send
	ContactId bigint NOT NULL,
	SendId bigint NOT NULL,
	SendType nvarchar(50) NOT NULL,
	EmailDesignVariantId bigint NULL,
	PatternId bigint NULL,

	-- Controlling how the send will be processed
	State tinyint NOT NULL, -- 0 = not processed yet, 1 = in progress, 2 = done
	Error tinyint NULL,
	Pause bit NOT NULL,
	Priority tinyint NOT NULL,

	-- Controlling which worker is sending this part
	Worker uniqueidentifier NULL,
	Chunk bigint NULL
)



ALTER TABLE [dbo].[TDX_EmailSendQueue]
ADD CONSTRAINT [PK_TDX_EmailSendQueue]
    PRIMARY KEY CLUSTERED (
	[SendId],
	[ContactId],
	[SendType]
)



create table TDX_EmailSend(
	Id bigint PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	EmailDesignId bigint NOT NULL,
    EmailPatternId bigint NOT NULL,
	ContactId bigint NULL,
	SendTime_UTC datetime2 NOT NULL,
	DeliveredTime_UTC datetime2 NOT NULL,
	-- TBD bounce handling ?
	EmailDesignVariantId bigint NOT NULL,
	EmailAddressId [bigint] NULL
)



create table TDX_UserAgent(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	-- This is a varchar instead of an nvarchar specifically because it must be indexed, and the max length of a unique constraint index is 900 bytes.
	UserAgentString varchar(900),
	CommonName nvarchar (50),
	Browser nvarchar (50),
	OperatingSystem nvarchar (50)
)



create table TDX_Domain (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	DomainName nvarchar(400)
)




create table TDX_EmailOpen(
    Id bigint PRIMARY KEY IDENTITY NOT NULL,
	EmailSendId bigint, -- Shouldn't this be NOT NULL as well?
	OpenTime_UTC datetime2 NOT NULL,
	IPAddress varbinary(16),
	UserAgentId bigint,
	ReferrerDomainId bigint
	-- TBD - also store account, email, contact
)



create table TDX_EmailLink(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	LinkName nvarchar(200),
	EmailPatternId bigint,
	EmailPatternComponent nvarchar(4), -- HTML, TEXT
	EmailPatternLinkId int, -- the ID of the link within the pattern and component
	LinkUrlPattern nvarchar(max)
)




create table TDX_EmailLinkClick(
    Id bigint PRIMARY KEY IDENTITY NOT NULL,
	EmailSendId bigint NOT NULL,
	LinkId bigint NOT NULL,
	DynamicLinkId int NOT NULL,
	IsOnline bit NOT NULL,
	ClickTime_UTC datetime2 NOT NULL,
	IPAddress varbinary(16),
	UserAgentId bigint,
	ReferrerDomainId bigint
	-- TBD - also store account, email, contact
)



IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[TestEmailsSequence]') AND type = 'SO')
drop SEQUENCE TestEmailsSequence



CREATE SEQUENCE TestEmailsSequence
AS bigint
START WITH 1
INCREMENT BY 1
CACHE 50



create table TDX_EmailSendFailure (
	Id bigint PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	EmailDesignId bigint NOT NULL,
	EmailPatternId bigint NULL,
	ContactId bigint NULL,
	AttemptedSendTime_UTC datetime2 NULL,
	Reason tinyint NOT NULL,
	Error tinyint NULL,
	EmailDesignVariantId bigint NOT NULL
)



create table TDX_SMSAggregator (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	AccountId bigint NOT NULL,
	AggregatorType nvarchar(50) NOT NULL,
	[Username] [nvarchar](255) NULL,
	[Password] [nvarchar](255) NULL,
	[Starttime] [time](7) NULL,
	[Endtime] [time](7) NULL,
	[APIURL] nvarchar(max) NULL,
	[SenderName] nvarchar(255) NULL,
	[DedicatedNumber] nvarchar(255) NULL
)



create table TDX_Configuration (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[Key] nvarchar(255) NOT NULL,
	[Value] nvarchar(255) NOT NULL
)



create table TDX_SMSAggregatorConfig (
	AggregatorId bigint NOT NULL,
	ConfigurationId bigint NOT NULL
)



ALTER TABLE [dbo].[TDX_SMSAggregatorConfig]
ADD CONSTRAINT [PK_TDX_SMSAggregatorConfig]
    PRIMARY KEY CLUSTERED (
	AggregatorId,
	[ConfigurationId]
)




---This table will be copied to TDX_SMSResult
create table TDX_SMSMessage (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[From] varchar(50) NULL,
	[To] nvarchar(255) NOT NULL,
	[Text] nvarchar(2000) NOT NULL,
	[Status] nvarchar(50) NULL,	
 	AggregatorType nvarchar(50) NULL,
	AccountId bigint NOT NULL,
	When_UTC datetime2 NULL,
	SMSDesignId bigint NULL,
	AggregatorId bigint NULL,
	ContactId bigint NULL,
	RetryAttempts int NULL,
	SendStartTimeLimit datetime2 NULL,
	SendEndTimeLimit datetime2 NULL,
	SendDay datetime2 NULL,
	Reference nvarchar(50) NULL,
	CampaignId bigint NULL,
	TriggeredSendId bigint NULL,
	IncomingSMSActionId BIGINT
)



create table TDX_SMSResult (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[From] varchar(50) NULL,
	[To] nvarchar(255) NOT NULL,
	[Text] nvarchar(2000) NOT NULL,
	[Status] nvarchar(50) NULL,	
 	AggregatorType nvarchar(50) NULL,
	AccountId bigint NOT NULL,
	When_UTC datetime2 NULL,
	SMSDesignId bigint NULL,
	AggregatorId bigint NULL,
	ContactId bigint NULL,
	RetryAttempts int NULL,
	SendStartTimeLimit datetime2 NULL,
	SendEndTimeLimit datetime2 NULL,
	SendDay datetime2 NULL,
	Reference nvarchar(50) NULL,
	CampaignId bigint NULL,
	TriggeredSendId bigint NULL,
	SMSMessageId bigint NULL,
	IncomingSMSActionId BIGINT
)



create table TDX_SMSDesign
(
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	AccountId bigint NOT NULL,
	Encoding nvarchar(50) NOT NULL,
	Content nvarchar(max) NOT NULL,
	Tested datetime2 NULL,
	AggregatorType nvarchar(50) NOT NULL,	
	AggregatorId bigint NOT NULL,
	ToNumberContactFieldId bigint NOT NULL
)



create table TDX_SMSProcessingQueue (
	Id bigint NOT NULL,

	-- About the SMS to send
	ContactId bigint NOT NULL,
	SendId bigint NOT NULL,
	PatternId bigint NULL,
	SendType nvarchar(50) NOT NULL,

	-- Controlling how the send will be processed
	State tinyint NOT NULL, -- 0 = not processed yet, 1 = in progress, 2 = done
	Error tinyint NULL,
	Pause bit NOT NULL,
	Priority tinyint NOT NULL,

	-- Controlling which worker is sending this part
	Worker uniqueidentifier NULL,
	Chunk bigint NOT NULL
)



ALTER TABLE [dbo].[TDX_SMSProcessingQueue]
ADD CONSTRAINT [PK_TDX_SMSProcessingQueue]
    PRIMARY KEY CLUSTERED (
	[SendId],
	[ContactId],
	[SendType]
)



create table TDX_SMSCampaignProcessingFailure (
	Id bigint PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	SMSDesignId bigint NOT NULL,
	SMSPatternId bigint NOT NULL,
	ContactId bigint NOT NULL,
	FailedTime_UTC datetime2 NULL,
	Reason tinyint NOT NULL,
	Error tinyint NULL
)



create table TDX_SMSCampaignProcessed(
	Id bigint PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	SMSDesignId bigint NOT NULL,
    SMSPatternId bigint NOT NULL,
	ContactId bigint NOT NULL,	
	Processed_UTC datetime2 NOT NULL
	-- TBD bounce handling ?
)



create table TDX_SMSResultPart(
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	ResultId bigint NOT NULL,
	UDH nvarchar(50) NULL,
	ErrorMessage nvarchar(255) NULL,
	ResultCode int NULL,
	When_UTC datetime2 NOT NULL,
	[Status] nvarchar(50) NOT NULL
)



create table TDX_SMSIncomingMessage(
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	MessageId bigint NULL,
	Content nvarchar(2000) NOT NULL,
	Reference nvarchar(50) Null,
	RawData nvarchar(max) NOT Null,
	When_UTC datetime2 NOT NULL,
	SMSIncomingActionId BIGINT NULL,
	Originator NVARCHAR(255),
	ContactId BIGINT
)



create table TDX_EventQueue (
	Id bigint PRIMARY KEY IDENTITY(1, 1),
	AccountId bigint NOT NULL,
	EventInstanceType varchar(50) NOT NULL,
	Queued datetime2 NOT NULL,
	Processed bit NOT NULL,
	ProcessAfter datetime2 NULL,
	Payload nvarchar(max) NOT NULL
)



create table TDX_EventConfiguration (
	Id bigint PRIMARY KEY IDENTITY(1,1),
	AccountId bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	Description nvarchar(255) NOT NULL,
	EventInstanceType varchar(50) NOT NULL,
	EventConfigurationType varchar(50) NOT NULL,
	Version int NOT NULL,
	Enabled bit NOT NULL,
	Configuration nvarchar(max) NOT NULL,
	CreatedOn datetime2 NOT NULL,
	ModifiedOn datetime2 NOT NULL
)



create table TDX_TriggeredSend (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	Name nvarchar(255) NOT NULL,
	Description nvarchar(255) NULL,
	AccountId bigint NOT NULL,	
	Type nvarchar(50) NOT NULL, -- Email or SMS
	DesignId bigint NULL, -- This is either EmailDesignId or SmsId, depending on Type
	Created datetime2 NOT NULL,
	CreatedBy bigint NOT NULL,
	Modified datetime2 NOT NULL,
	ModifiedBy bigint NOT NULL,
	CampaignAnalyticsId BIGINT NULL, 
	CampaignLitmusAnalyticsId BIGINT NULL,
	SubDomainId BIGINT NULL
)



create table TDX_EventBehaviourAudit (
	Id bigint PRIMARY KEY IDENTITY,
	ConfigurationId bigint NOT NULL,
	ConfigurationVersion int NOT NULL,
	InstanceId bigint NOT NULL,
	-- This can be a varchar (i.e. not nvarchar), as it is not user data. This will be set by the system and will not contain unicode.
	BehaviourId varchar(30) NOT NULL,
	Result bit NOT NULL,
	Occurred_UTC datetime2 NOT NULL
)



create table TDX_AutoResponder
(
	Id bigint PRIMARY KEY IDENTITY(1,1),
	Name nvarchar(255) NOT NULL,
	[Description] nvarchar(255) NULL,
	SendId bigint NOT NULL,
	AutoResponderType nvarchar(50) NOT NULL,
	CriteriaId bigint NULL,
	Active bit NOT NULL,
	AccountId bigint NOT NULL,
	CreatedOn datetime2 NOT NULL,
	ModifiedOn datetime2 NOT NULL,
	ScheduleType nvarchar(50) NOT NULL,
	ScheduleData nvarchar(255) NULL,
	SendHour tinyint NOT NULL,
	SendMinute tinyint NOT NULL,
	AllowEmailDuplicate BIT NOT NULL,
	Type nvarchar(50) NOT NULL
)



create table TDX_LandingPage
(
	[Id] bigint PRIMARY KEY IDENTITY(1,1),
	[AccountId] bigint NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[Description] nvarchar(255) NULL,
	[Content] nvarchar(max) NULL,
	[PublicId] nvarchar(255) NOT NULL,
	[Editor] nvarchar(50) NOT NULL,
	LandingPageType nvarchar(50) NOT NULL
)



create table TDX_Role
(
	Id bigint PRIMARY KEY,
	PermissionId bigint,
	ResourceId bigint
)



create table TDX_UserInstanceRole
(
	RoleId bigint NOT NULL,
	UserInstanceId bigint NOT NULL
)



ALTER TABLE [dbo].[TDX_UserInstanceRole]
ADD CONSTRAINT [PK_TDX_UserInstanceRole]
	PRIMARY KEY CLUSTERED(
	[RoleId],
	[UserInstanceId]
)



create table TDX_Permission
(
	Id bigint PRIMARY KEY IDENTITY(1,1),
	Name nvarchar(255) NOT NULL
)



create table TDX_Resource
(
	Id bigint PRIMARY KEY,
	Name nvarchar (255)
)



create table TDX_Bounce
(
	[Id] bigint PRIMARY KEY IDENTITY(1,1),
	[AccountId] [bigint] NOT NULL,
	[EmailSendId] [bigint] NOT NULL,
	[ContactId] bigint NULL, -- Potentially null, because we may get a bounce from a transactional email where we don't have a persistant contact.
	[BounceType] [varchar](10) NOT NULL,
	[SenderEmail] [nvarchar](255) NOT NULL,
	[RecipientEmail] [nvarchar](255) NOT NULL,
	[BounceReason] [nvarchar](1000) NOT NULL,
	[DSNDiagnostic] [nvarchar](1000) NULL,
	[TimeLogged] [datetime2](7) NOT NULL,
	[Timestamp] [datetime2](7) NOT NULL
)




create table TDX_FormField
(
	Id bigint identity primary key,
	FormId bigint not null,
	FieldId bigint null,
	FieldName nvarchar(1000) not null,
	FormFieldType varchar(50) not null,
	FormFieldInputType varchar(50) NULL
)



create table TDX_EmailBlockList
(
	Id bigint IDENTITY PRIMARY KEY,
	EmailAddress nvarchar(255) NOT NULL,
	AccountId bigint NOT NULL,
	-- Auditing fields
	Blocked datetime2 NOT NULL,
	BlockSource varchar(50) NOT NULL, -- ENUM field
	BlockedByUserInstanceId bigint NULL,
	-- this is 50 long because we may need to store IPv6 addresses in future
	BlockedByIPAddress varchar(50)
)




CREATE TABLE [dbo].[TDX_APIUser](
	[Id] [bigint] IDENTITY(1,1) primary key NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[ApplicationName] [varchar](255) NOT NULL,	
	[Token] [varchar](255) NOT NULL,
	[Created] [datetime2](7) NOT NULL,
	[CreatedBy] [bigint] NOT NULL,
	[IsActive] [bit] NOT NULL
)



CREATE TABLE [dbo].[TDX_APICallAudit](
	[Id] [bigint] IDENTITY(1,1) primary key NOT NULL,
	[APIUserId] [bigint] NOT NULL,
	[HttpMethod] [varchar](256) NULL,
	[RequestURI] [varchar](1000) NULL,
	[IPAddress] [varchar](256) NULL,
	[Timestamp] [datetime2](7) NOT NULL
	)


CREATE TABLE [dbo].[TDX_CampaignAnalytics](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY NOT NULL,
	[Medium] [nvarchar](255) NOT NULL,
	[CampaignName] [nvarchar](255) NOT NULL,
	[Source] [nvarchar](255) NOT NULL,
	[Content] [nvarchar](255) NULL,
	[Enabled] [bit] NOT NULL,
	[Modified] [datetime2](7) NOT NULL
	)


CREATE TABLE [dbo].[TDX_ImportContactSetting](
	[Id] [bigint] IDENTITY(1,1) Primary key NOT NULL,
	[ContactFieldId] [bigint] NULL,
	[ContactListId] [bigint] NULL,
	[FieldDelimiter] nvarchar(255) NOT NULL,
	[RecordDelimiter] nvarchar(255) NULL,
	[AllowInsert] [bit] NOT NULL,
	[AllowUpdate] [bit] NOT NULL,
	[PreserveRemovalsFromList] [bit] NOT NULL,
	[RejectDuplicates] [bit] NOT NULL,
	[MarkAsActive] [bit] NULL,
	[ColumnHeaderMapping] nvarchar(max) NULL,
	[LocalFilePath] nvarchar(1024) NULL,
	[OriginalFileName] nvarchar(1024) NULL,
	[AssoicateContactList] [bit] NOT NULL,
	DateFormat nvarchar(3) not NULL,
	SkipEmptyField bit not NULL
) 


CREATE TABLE [dbo].[TDX_ImportContactQueue](
	[Id] [bigint]  IDENTITY(1,1) Primary key NOT NULL,
	[AccountId] [bigint] Not NULL,
	[UserInstanceId] [bigint] NULL,
	[ImportContactSettingId] [bigint] Not NULL,
	[JobState] nvarchar(255) NOT NULL,
	[JobType] nvarchar(255) NOT NULL,
	[CreationDate] [datetime] NOT NULL,
	[StartDate] [datetime] NULL,
	[CompletedDate] [datetime] NULL,
	[JobProgress] [smallint] NOT NULL,
	[APIUserId] [bigint] NULL,
	[SFTPSchedulerId] [bigint]  NULL,
	[PriorityQueue] int null
)


CREATE TABLE [dbo].[TDX_ContactImportResult](
	[Id] [bigint]  IDENTITY(1,1) Primary key NOT NULL,
	[ImportContactQueueId] [bigint] Not NULL,
	[InsertedCount] [bigint] NULL,
	[UpdatedCount] [bigint] NULL,
	[ErrorCount] [bigint] NULL,
	[ErrorOutputPath] [nvarchar](1024) NULL
)




CREATE TABLE TDX_EmailUnsubscribe (
	Id bigint IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	CampaignId bigint  NULL,
	ContactId bigint NOT NULL,
	EmailSendId bigint NOT NULL,
	IpAddress nvarchar(50) NOT NULL,
	UnsubscribeTime_UTC datetime2 NOT NULL,
	EmailUnsubscribeType nvarchar(50) NOT NULL,
	ListId bigint NULL,
	BlockedAddress nvarchar(255) NULL,
	Resubscribed bit NOT NULL,
	TriggeredSendId BIGINT NULL
)



create table TDX_Tag (
	Id bigint IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	Name nvarchar(255) NOT NULL,
	AccountId bigint NOT NULL
)




create table TDX_TagMember (
	TagId bigint NOT NULL,
	MemberId bigint NOT NULL,
	MemberType nvarchar(50) NOT NULL
)



ALTER TABLE TDX_TagMember
ADD CONSTRAINT PK_TDX_TagMember
PRIMARY KEY CLUSTERED (
	TagId,
	MemberId,
	MemberType
)



CREATE TABLE TDX_AccountSetting (
	Id bigint IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	FromName nvarchar(255) NULL,
	FromAddress nvarchar(255) NULL,
	UnSubscriptionLandingPageId BIGINT NULL,
	[ReCaptchaSiteKey] nvarchar(512) Null,
	[ReCaptchaSecretKey] nvarchar(512) Null,
	[SMSUnsubscribeKeyword] nvarchar(255) Null,
	[SMSUnsubscribeType] nvarchar(255) NOT Null,
	ReportDiskSpaceLimit bigint NULL,
	ReSubscriptionLandingPageId BIGINT NULL,
	ConfigurablePassword BIT NOT NULL,
	SubDomainId bigint null,
	GDPREnabled bit not null,
	GDPRControllerMessage nvarchar(500) null
)



CREATE TABLE [dbo].[TDX_ExternalContent](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY  NOT NULL,
	[AccountId] bigint NOT NULL,	
	[URL] [nvarchar](max) NOT NULL,
	[Content] [nvarchar](max) NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[ModifiedOn] [datetime2](7) NOT NULL
)



CREATE TABLE [dbo].[TDX_ScheduledReport](
	[Id] [bigint] PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[JobName] [nvarchar](255) NOT NULL,
	[JobGroup] [nvarchar](150) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[UserInstanceId] [bigint] NOT NULL
)


CREATE TABLE [dbo].[TDX_CampaignLitmusAnalytics](
	[Id] [bigint] PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[Enabled] [bit] NOT NULL,
	[TrackingCode] [nvarchar](max) NOT NULL,
	[Modified] [datetime2](7) NOT NULL
)



create table TDX_SystemContent (
	Name nvarchar(50) PRIMARY KEY NOT NULL,
	Active bit NOT NULL,
	Content nvarchar(max) NOT NULL
)



CREATE TABLE TDX_SubDomain
(
	[Id] [bigint] IDENTITY(1,1) NOT NULL primary key, 
	[AccountId] [bigint] NOT NULL,  
	[Name] [nvarchar](255) NOT NULL,  --display name
	[Description] [nvarchar](512) NULL, -- display description
	[Subdomain] [nvarchar](255) NOT NULL, -- subdomain name
	[VirtualMTA] [nvarchar](255) NOT NULL, -- virtualMTA
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,
	[Active] [bit] NOT NULL
)



create table TDX_LoginAudit (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	UserInstanceId bigint NOT NULL,
	Ocurred datetime2 NOT NULL
)



CREATE TABLE [dbo].[TDX_ContactSubscriptionAudit] (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	ContactId bigint NOT NULL,
	AccountId bigint NOT NULL,
	Subscribed bit NOT NULL,
	ChangeDate datetime2 NOT NULL,
	AgentType tinyint NOT NULL,
	UserInstanceId bigint NULL,
	SourceIP varchar(45) NULL,
	ApiUserId bigint NULL,
	SMSIncomingActionId BIGINT NULL
)



CREATE TABLE [dbo].[TDX_EmailAlias](
	[Id] [bigint] IDENTITY(1,1)  PRIMARY KEY NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[SubDomainId] [bigint] NOT NULL,
	[Alias] [nvarchar](255) NOT NULL,
	[RedirectTo] [nvarchar](MAX) NOT NULL
)


-- Identity seed for measure definitions is 10,000. Global measures occupy the low id space.
CREATE TABLE [dbo].[TDX_MeasureDefinition](
	[Id] [bigint] IDENTITY(10000,1)  PRIMARY KEY NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[DataType] varchar(50) NOT NULL,
	[SqlTemplate] [nvarchar](MAX) NOT NULL,
	[Active] bit NOT NULL,
	[AccountId] [bigint] NULL,
    [Description] nvarchar(512) NULL
)


CREATE TABLE [dbo].[TDX_MeasureDefinition_Parameter](
	[Id] [bigint] IDENTITY(1,1)  PRIMARY KEY NOT NULL,
	[MeasureDefinitionId] [bigint] NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[DataType] varchar(50) NOT NULL,
	[Mandatory] bit NOT NULL,
	PredefinedValues NVARCHAR(512) NULL,
    [Description] nvarchar(512) NULL	
)


alter table [TDX_MeasureDefinition_Parameter]
add constraint FK_TDX_MeasureDefinition_Parameter_MeasureDefinitionId
FOREIGN KEY (MeasureDefinitionId)
references TDX_MeasureDefinition (Id)



CREATE TABLE [dbo].[TDX_ContactMeasure](
	[Id] [bigint] IDENTITY(1,1)  PRIMARY KEY NOT NULL,
	[MeasureDefinitionId] [bigint] NOT NULL
)


alter table TDX_ContactMeasure
add constraint FK_TDX_ContactMeasure_MeasureDefinitionId
FOREIGN KEY (MeasureDefinitionId)
references TDX_MeasureDefinition (Id)



CREATE TABLE [dbo].[TDX_ContactMeasure_Parameter](
	[Id] [bigint] IDENTITY(1,1)  PRIMARY KEY NOT NULL,
	[ContactMeasureId] [bigint] NOT NULL,
	[MeasureDefinition_ParameterId] [bigint] NOT NULL,
    [StringValue] nvarchar(255) NULL,
	[NumberValue] numeric(28,5) NULL,
	[DateValue] datetime2 NULL,
	[BooleanValue] bit NULL
)


alter table TDX_ContactMeasure_Parameter
add constraint FK_TDX_ContactMeasure_Parameter_ContactMeasureId
FOREIGN KEY (ContactMeasureId)
references TDX_ContactMeasure (Id)



alter table TDX_ContactMeasure_Parameter
add constraint FK_TDX_ContactMeasure_Parameter_MeasureDefinition_ParameterId
FOREIGN KEY (MeasureDefinition_ParameterId)
references TDX_MeasureDefinition_Parameter (Id)



ALTER TABLE TDX_Criteria_Expression
add constraint FK_TDX_Criteria_Expression_ContactMeasureId
FOREIGN KEY (ContactMeasureId)
references TDX_ContactMeasure (Id)



CREATE TABLE [TDX_SMSBlockList]
(
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY NOT NULL,
	[MobileNumber] [nvarchar](255) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Blocked] [datetime2](7) NOT NULL,
	[BlockSource] [varchar](50) NOT NULL,
	[BlockedByUserInstanceId] [bigint] NULL,
	[BlockedByIPAddress] [varchar](50) NULL
)



ALTER TABLE TDX_SMSBlockList
ADD CONSTRAINT UNQ_TDX_SMSBlockList_MobileNumber_AccountId
UNIQUE (MobileNumber, AccountId)



CREATE TABLE [dbo].TDX_ABTestResult
(
	[Id] [bigint] IDENTITY(1,1)  PRIMARY KEY NOT NULL,
	CampaignId [bigint] NOT NULL,
	EmailDesignVariantId [bigint]  NOT NULL,
	ABSendRule nvarchar(50) NOT NULL,
	ABSendHours int NOT NULL,
	ABPercentage int NOT NULL,
	VariantLabel nvarchar(50) NOT NULL,
	TestDeliveried [bigint]  NOT NULL,
	TestOpenRate [bigint] NOT NULL,
	TestClickRate [bigint]  NOT NULL,
	ManualLaunchUserInstanceId [bigint] NULL,
	IsLaunched bit NOT NULL
)


CREATE TABLE TDX_SMSUnsubscribe (
	Id bigint IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	CampaignId bigint NULL,
	ContactId bigint NOT NULL,
	SMSMessageId bigint NOT NULL,
	SMSUnsubscribeType nvarchar(50) NOT NULL,
	BlockedNumber nvarchar(255) NULL,
	SMSReplyId bigint NOT NULL,
	MobileNumber nvarchar(255) NULL,
	UnsubscribeTime_UTC datetime2 NOT NULL,
	TriggeredSendId BIGINT NULL
)


CREATE TABLE [dbo].[TDX_ApiEvent](
	[EventId] [uniqueidentifier] NOT NULL,
	[Version] [bigint] IDENTITY(1,1) NOT NULL,
	[ApiUserId] [bigint] NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Created] [datetime2] NOT NULL,
	[Name] [nvarchar](100) NOT NULL
)



CREATE TYPE [dbo].[TDX_TYPE_ApiEventType] AS TABLE(
	[EventId] [uniqueidentifier] NOT NULL,
	[ApiUserId] [bigint] NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Created] [datetime] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[SortOrder] [int] NOT NULL,
	PRIMARY KEY CLUSTERED
	(
		[SortOrder] ASC
	)
	WITH (IGNORE_DUP_KEY = OFF)
)

CREATE TABLE [dbo].[TDX_FileTransferSchedule](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[Description] [nvarchar](255) NULL,
	[TransferType] [nvarchar](50) NOT NULL,
	[ScheduleType] [nvarchar](50) NOT NULL,
	[ScheduleData] [nvarchar](255) NULL,
	[SendHour] [tinyint] NOT NULL,
	[SendMinute] [tinyint] NOT NULL,	
	[CreatedOn] [datetime2](7) NOT NULL,
	[ModifiedOn] [datetime2](7) NOT NULL,
	[Active] [bit] NOT NULL,
	FileProfileId [BIGINT]
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) 
)



CREATE TABLE [dbo].[TDX_CustomInteraction](
	[Id] [bigint] IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[ContactId] [bigint] NOT NULL,
	[TimeStamp] [datetime2](7) NOT NULL,
	[Name] [nvarchar](50) NOT NULL,
	[TextData] [nvarchar](255) NULL,
	[NumericData] [numeric](28, 5) NULL
 )

create nonclustered index IDX_TDX_CustomInteraction_AccountId_Name on TDX_CustomInteraction
(
	AccountId,
	Name,
	TimeStamp
)
include
(
	Id,
	ContactId,
	TextData,
	NumericData
)



CREATE TABLE [dbo].[TDX_ImportCustomInteractionQueue](
	[Id] [bigint] IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[UserInstanceId] [bigint] NULL,
	[ImportCustomInteractionSettingId] [bigint] NOT NULL,
	[JobState] [nvarchar](255) NOT NULL,
	[JobType] [nvarchar](255) NOT NULL,
	[CreationDate] [datetime] NOT NULL,
	[StartDate] [datetime] NULL,
	[CompletedDate] [datetime] NULL,
	[JobProgress] [smallint] NOT NULL,
	ApiUserId BIGINT NULL
)




CREATE TABLE [dbo].[TDX_ImportCustomInteractionSetting](
	[Id] [bigint] IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	[IdentifierFieldId] [bigint] NULL,
	[FieldDelimiter] [nvarchar](255) NOT NULL,
	[RecordDelimiter] [nvarchar](255) NULL,
	[ColumnHeaderMapping] [nvarchar](max) NULL,
	[LocalFilePath] [nvarchar](1024) NULL,
	[OriginalFileName] [nvarchar](1024) NULL,
	[DateFormat] [nvarchar](3) NOT NULL,
	[GoogleAnalyticReportSettingId] [bigint]  NULL
)



CREATE TABLE [dbo].[TDX_CustomInteractionImportResult](
	[Id] [bigint]  IDENTITY(1,1) Primary key NOT NULL,
	[ImportCustomInteractionQueueId] [bigint] Not NULL,
	[InsertedCount] [bigint] NULL,
	[ErrorCount] [bigint] NULL,
	[ErrorOutputPath] [nvarchar](1024) NULL
)


IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ContactIdSequence]') AND type = 'SO')
drop SEQUENCE ContactIdSequence



CREATE SEQUENCE ContactIdSequence
AS bigint
START WITH 1
INCREMENT BY 1
CACHE 50



CREATE TABLE [dbo].[TDX_CampaignApprover](
	[Id] [bigint] IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[UserInstanceId] [bigint] NOT NULL
)



CREATE TABLE [dbo].[TDX_CampaignApprovalAudit](
	[Id] [bigint] IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	[CampaignId] [bigint] NOT NULL,
	[RequestedBy] [bigint] NOT NULL,
	[Requested_UTC] [datetime2] NOT NULL,
	[Response] varchar(50) NULL,
	[ResponseBy] [bigint] NULL,
	[Response_UTC] [datetime2] NULL,
	[Message] nvarchar(1000) NULL
)



CREATE TABLE [dbo].[TDX_AccountInternalSetting](
	[Id] [bigint] IDENTITY(1, 1) PRIMARY KEY NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[LaunchApprovalEnabled] [bit] NOT NULL,
	[Location] nvarchar(255) NOT NULL,
	CarbonCopyEnabled bit NOT NULL,
	FileStorage nvarchar(50) NOT NULL,
	FileStorageSizeLimitInMB int NOT NULL,
	[OverrideTheme] bit NULL,
	[UseAncestorTheme] bit NULL,
	[ThemeLocation] nvarchar(512) NULL
)



ALTER TABLE [dbo].[TDX_AccountInternalSetting] ADD CONSTRAINT DF_DEFAULT_TDX_AccountInternalSetting_OverrideTheme DEFAULT 0 FOR [OverrideTheme];
ALTER TABLE [dbo].[TDX_AccountInternalSetting] ADD CONSTRAINT DF_DEFAULT_TDX_AccountInternalSetting_UseAncestorTheme DEFAULT 1 FOR [UseAncestorTheme]



CREATE TABLE [dbo].[TDX_CustomReport](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[Notes] [nvarchar](max) NULL,
	[SQL] [nvarchar](max) NOT NULL
) 




CREATE TABLE [dbo].[TDX_CustomReportAccount](
	[AccountId] [bigint] NOT NULL,
	[CustomReportId] [bigint] NOT NULL,
)



ALTER TABLE TDX_CustomReportAccount
ADD CONSTRAINT PK_TDX_CustomReportAccount
PRIMARY KEY CLUSTERED (
	AccountId,
	CustomReportId
)




create table TDX_Automation(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
	Version bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	Description nvarchar(1000) NULL,
	Active bit NOT NULL,
	ExcludeContactEntryWhileInProcess bit NOT NULL,
	MinContactRepeatDays int NOT NULL,
	CreatedOn datetime2 NOT NULL,
	ModifiedOn datetime2 NOT NULL,
	SerializedConfiguration nvarchar(max) NOT NULL
)



create table TDX_AutomationListenerWait(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
    AutomationId bigint NOT NULL,
    AutomationWorkItemId bigint NOT NULL,
    AutomationNodeId bigint NOT NULL,
    AutomationRuleId bigint NOT NULL,
    WaitUntilUTC datetime2 NOT NULL
)



create table TDX_AutomationTriggerEmailOpen(
	AutomationId bigint PRIMARY KEY NOT NULL,
	EmailDesignId bigint NOT NULL
)



create table TDX_AutomationListenerEmailOpen(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
    AutomationId bigint NOT NULL,
    AutomationWorkItemId bigint NOT NULL,
    AutomationNodeId bigint NOT NULL,
    AutomationRuleId bigint NOT NULL,
    WaitUntilUTC datetime2 NOT NULL,
	Inverse bit NOT NULL,
	EmailSendId bigint NOT NULL
)



create table TDX_AutomationTriggerEmailClick(
	AutomationId bigint PRIMARY KEY NOT NULL,
	EmailDesignId bigint NOT NULL,
	NameLike nvarchar(1000) NULL,
	UrlLike nvarchar(1000) NULL
)



create table TDX_AutomationListenerEmailClick(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
    AutomationId bigint NOT NULL,
    AutomationWorkItemId bigint NOT NULL,
    AutomationNodeId bigint NOT NULL,
    AutomationRuleId bigint NOT NULL,
    WaitUntilUTC datetime2 NOT NULL,
	Inverse bit NOT NULL,
	EmailSendId bigint NOT NULL,
	NameLike nvarchar(1000) NULL,
	UrlLike nvarchar(1000) NULL
)



create table TDX_AutomationTriggerFormSubmit(
	AutomationId bigint PRIMARY KEY NOT NULL,
	FormId bigint NOT NULL
)



create table TDX_AutomationListenerFormSubmit(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
    AutomationId bigint NOT NULL,
    AutomationWorkItemId bigint NOT NULL,
    AutomationNodeId bigint NOT NULL,
    AutomationRuleId bigint NOT NULL,
    WaitUntilUTC datetime2 NOT NULL,
	Inverse bit NOT NULL,
	ContactId bigint NOT NULL,
	FormId bigint NOT NULL
)



create table TDX_AutomationWorkItemAudit(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AutomationId bigint NOT NULL,
	Version bigint NOT NULL,
	AutomationWorkItemId bigint NOT NULL,
	AutomationRuleId bigint NOT NULL,
	AutomationNodeId bigint NOT NULL,
	When_UTC datetime2 NOT NULL,
	ActionResult int NOT NULL
)



create table TDX_AutomationWorkItem(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
    AutomationId bigint NOT NULL,
    ContactId bigint NULL,
    Payload nvarchar(max),
    ProcessState tinyint NOT NULL,
    CreatedOnUTC datetime2 NOT NULL,
    CompletedOnUTC datetime2 NULL,
	AutomationVersion bigint NOT NULL,
	AutomationEventQueueId bigint NULL
)



create table TDX_AutomationEventQueue (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	EventType int NOT NULL,
	Processed bit NOT NULL,
	Occurred_UTC datetime2 NOT NULL,
	AccountId bigint NOT NULL,
	Payload nvarchar(max) NOT NULL
)



create table TDX_AutomationEventQueueHistory (
	Id bigint PRIMARY KEY NOT NULL,
	EventType int NOT NULL,
	Processed bit NOT NULL,
	Occurred_UTC datetime2 NOT NULL,
	AccountId bigint NOT NULL,
	Payload nvarchar(max) NOT NULL
)




CREATE TABLE TDX_CustomReportInstance(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
	CreatedBy bigint NOT NULL,
	Created_UTC datetime2 NOT NULL,
	Name nvarchar(255) NULL,
	CustomReportId bigint NOT NULL,
	Active bit NULL,
	Type varchar(50) NOT NULL,
	ScheduleType nvarchar(50) NULL,
	ScheduleData nvarchar(255) NULL,
	SendHour tinyint NULL,
	SendMinute tinyint NULL
	) 



CREATE TABLE TDX_CustomReportResult(
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	CustomReportInstanceId bigint NOT NULL,
	AccountId bigint NOT NULL,
	ExecutionStart_UTC datetime2 NOT NULL,
	ExecutionFinish_UTC datetime2 NOT NULL,
	Status nvarchar (50) NOT NULL,
	DetailedError nvarchar(max) NULL
)



create table TDX_AutomationHistory(
	AutomationId bigint NOT NULL,
	Version bigint NOT NULL,
	AccountId bigint NOT NULL,
	Name nvarchar(255) NOT NULL,
	Description nvarchar(1000) NULL,
	ExcludeContactEntryWhileInProcess bit NOT NULL,
	MinContactRepeatDays int NOT NULL,
	ModifiedBy bigint NOT NULL,
	ModifiedOn datetime2 NOT NULL,
	SerializedConfiguration nvarchar(max) NOT NULL
)



ALTER TABLE TDX_AutomationHistory
ADD CONSTRAINT PK_TDX_AutomationHistory
PRIMARY KEY CLUSTERED (
	AutomationId,
	Version
)



create table TDX_AutomationStatusHistory (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AutomationId bigint NOT NULL,
	Version bigint NOT NULL,
	Status varchar(50) NOT NULL,
	ModifiedBy bigint NOT NULL,
	ModifiedOn datetime2 NOT NULL
)



create table TDX_RoiTrackEvent (
	[Id] bigint PRIMARY KEY IDENTITY NOT NULL,
	[TrackName] nvarchar(255) NOT NULL,
	[Occurred_UTC] datetime2 NOT NULL,
	[Test] bit NOT NULL,
	[MessageId] bigint NOT NULL,
	[Source] nvarchar(50) NOT NULL,
	[ContactId] bigint NOT NULL,
	[EmailSendId] bigint NOT NULL,
	[Value] numeric(28,5) NULL,
	[Text] nvarchar(255)  NULL,
	[TransactionId] nvarchar(50) NULL
)



CREATE TABLE [dbo].[TDX_FileProfile](
	[Id] [bigint] PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[TransferType] [nvarchar](255) NOT NULL,
	[ColumnHeaderMapping] [nvarchar](max) NOT NULL,
	[Active] [bit] NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[ModifiedOn] [datetime2](7) NOT NULL,
	[CreatedBy] [bigint] NOT NULL,
	[ModifiedBy] [bigint] NOT NULL,
	ContactIdentifierFieldId [bigint]
	)


create table TDX_CustomReportParameter (
	[Id] bigint PRIMARY KEY IDENTITY NOT NULL,
	[CustomReportId] bigint NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[ParameterType] varchar(50) NOT NULL,
	[Mandatory] bit NOT NULL
)



create table TDX_CustomReportParameterValue (
	[Id] bigint PRIMARY KEY IDENTITY NOT NULL,
	[CustomReportInstanceId] bigint NOT NULL,
	[ParameterName] nvarchar(255) NOT NULL,
	[DateTimeValue] datetime2 NULL,
	[StringValue] nvarchar(255) NULL,
	[DecimalValue] numeric(28,5) NULL,
	[BooleanValue] bit NULL,
	[IdValue] bigint null
)



CREATE TABLE [dbo].[TDX_ScheduledReportInstance] (
	[Id] bigint PRIMARY KEY IDENTITY NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[CreatedBy] [bigint] NOT NULL,
	[Created_UTC] [datetime2](7) NOT NULL,
	[Name] [nvarchar](255) NULL,
	[ReportId] [bigint] NULL,
	[Active] [bit] NOT NULL,
	[ReportType] [varchar](50) NOT NULL,
	[ScheduleType] [nvarchar](50) NULL,
	[ScheduleData] [nvarchar](255) NULL,
	[SendHour] [tinyint] NOT NULL,
	[SendMinute] [tinyint] NOT NULL,
	[Description] [nvarchar](255) NULL,
	[ModifiedOn] [datetime2](7) NULL,
	[FileProfileId] [bigint] NULL,
	[FTPServerName] [nvarchar](255) NULL,
	[FTPUserName] [nvarchar](255) NULL,
	[FTPPassword] [nvarchar](255) NULL,
	[FTPFolderLocation] [nvarchar](255) NULL,
	[FTPFileName] [nvarchar](50) NULL,
	[FTPFileExtension] [varchar](50) NULL,
	[Delimiter] [varchar](50) NULL,
	[EmailNotification] [bit] NULL,
	[Email] [nvarchar](255) NULL,
	[EmailSubject] [nvarchar](255) NULL,
	[EmailSuccessMessage] [nvarchar](255) NULL,
	[EmailFailureMessage] [nvarchar](255) NULL,
	[FTPNotification] [bit] NULL,
	[CriteriaId] [bigint] NULL,
	[ImportContactSettingId] [BIGINT] NULL,
	ImportCustomInteractionSettingId  BIGINT NULL,
	[ReportSource] [varchar](100) NULL
	)



CREATE TABLE TDX_FeedbackLoop
(
	Id BIGINT NOT NULL PRIMARY KEY IDENTITY(1,1) ,
	AccountId BIGINT NOT NULL,
	EmailSendId BIGINT NOT NULL,
	ContactId BIGINT NOT NULL,
	SenderEmail NVARCHAR(255) NOT NULL,
	RecipientEmail NVARCHAR(255) NOT NULL,
	UserAgent NVARCHAR(255) NOT NULL,
	[FeedbackFormat] NVARCHAR(255) NULL,
	FeedbackType NVARCHAR(255) NOT NULL, 
	TimeLogged DATETIME2 NOT NULL,
	[Timestamp] DATETIME2 NOT NULL
) 




CREATE TABLE [dbo].[TDX_UserGroup](
	[Id] bigint PRIMARY KEY IDENTITY,
	[Name] nvarchar(255) NOT NULL,
	[Description] nvarchar(max),
	[Global] bit NOT NULL,
	[AccountId] bigint,
	[ChildAccount] bit NOT NULL,

	[Created] datetime2 NOT NULL,
	[CreatedBy] bigint NOT NULL,
	[Updated] datetime2 NOT NULL,
	[UpdatedBy] bigint NOT NULL,

	[SubGroup] nvarchar(100)
)


ALTER TABLE [dbo].[TDX_UserGroup] ADD CONSTRAINT DF_DEFAULT_TDX_UserGroup_Global DEFAULT 1 FOR [Global]
ALTER TABLE [dbo].[TDX_UserGroup] ADD CONSTRAINT DF_DEFAULT_TDX_UserGroup_ChildAccount DEFAULT 1 FOR [ChildAccount]

CREATE TABLE [dbo].[TDX_UserGroupRole](
	[Id] bigint PRIMARY KEY IDENTITY,
	[UserGroupId] bigint NOT NULL,
	[RoleId] bigint NOT NULL
)


CREATE TABLE TDX_DashboardAccountStatistic (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
    Contacts bigint  NOT NULL,
	ActiveContacts bigint  NOT NULL,
	EngaggedContacts bigint  NOT NULL,
    CurrentWeekEmailSent bigint NOT NULL,
	PreviousWeekEmailSent bigint NOT  NULL,
	EmailSent bigint NOT NULL,
    CurrentMonthEmailClickRate DECIMAL(10,1) NOT NULL,
    PreviousMonthEmailClickRate DECIMAL(10,1) NOT NULL,
    CurrentMonthEmailOpenRate DECIMAL(10,1) NOT NULL,
    PreviousMonthEmailOpenRate DECIMAL(10,1) NOT NULL,
    AverageEmailOpenRate DECIMAL(10,1) NOT NULL,
    AverageEmailClickRate DECIMAL(10,1) NOT NULL,
	CurrentMonth tinyint NOT null,
	CurrentWeekSMSSent BIGINT, 
	PreviousWeekSMSSent BIGINT, 
	SMSSent BIGINT, 	
	SMSReply bigint,
	[BlockedEmails] [bigint] NULL,
	[BlockedMobilenumbers] [bigint] NULL,
	[BlockedCount] [bigint] NULL,
	[CurrentMonthNewContacts] [bigint] NULL,
	[PreviousMonthNewContacts] [bigint] NULL,
	[CurrentMonthSubscribers] [bigint] NULL,
	[PreviousMonthSubscribers] [bigint] NULL,
	Source varchar(255) NOT NULL,
    Updated datetime2 NOT NULL
)


create table [TDX_ContactFieldDeleteQueue] (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
	ContactFieldId bigint NOT NULL,
	DeleteInitiated_UTC datetime2 NOT NULL,
	Deleted bit NOT NULL,
	DeleteCompleted_UTC datetime2 NULL
)




CREATE TABLE TDX_SMSMessageLookup (
	Id bigint PRIMARY KEY IDENTITY(1,1) NOT NULL,
	DedicatedNumber nvarchar(255) NOT NULL,
	MobileNumber nvarchar(255) NOT NULL,
	SMSMessageId bigint NOT NULL,
	AccountId bigint Not NULL,
	Updated datetime2 NOT NULL
)


CREATE TABLE TDX_CampaignStatistic (
	Id bigint PRIMARY KEY IDENTITY,
	CampaignId bigint NOT NULL,
	AccountId bigint NOT NULL,
	ContactCount bigint NULL,
	OpenRate DECIMAL(10,1) NULL,
	ClickRate DECIMAL(10,1) NULL,
	Updated datetime2 not null
)


CREATE TABLE TDX_GoogleAnalyticReportSetting(
	[Id] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[IdentifierFieldId] [bigint] NOT NULL,
	[ViewId] [nvarchar](255) NOT NULL,
	[PrimaryDimension] [nvarchar](255) NOT NULL,
	[SecondaryDimension] [nvarchar](255) NOT NULL,
	[ServiceAccountKey] [nvarchar](max) NOT NULL
) 


	CREATE TABLE [dbo].[TDX_GoogleAnalyticReportSetting_Metric]
	(
	Id bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[SettingId] [bigint] NOT NULL,
	[MetricName] [nvarchar](255) NOT NULL,
	[MetricAlias] [nvarchar](255) NOT NULL,
	[Position] [int] NOT NULL
	)


	/* Swipe n go tables start*/

CREATE TABLE [dbo].[TDX_Service](
	[Id] bigint PRIMARY KEY IDENTITY,
	[ServiceCode] nvarchar(255) NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[Description] nvarchar(max),
	[IsEnterprise] bit NOT NULL,
	[IsCommunication] bit NOT NULL,
	[OverAge] bit NOT NULL,
	[ManualConsumption] bit NOT NULL,
	[SequenceNo] int NOT NULL,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL
)


alter table TDX_Service
add constraint UNQ_TDX_Service_ServiceCode
unique (ServiceCode)


ALTER TABLE [dbo].[TDX_Service] ADD CONSTRAINT DF_DEFAULT_TDX_Service_IsEnterprise DEFAULT 0 FOR [IsEnterprise];
ALTER TABLE [dbo].[TDX_Service] ADD CONSTRAINT DF_DEFAULT_TDX_Service_IsCommunication DEFAULT 0 FOR [IsCommunication];
ALTER TABLE [dbo].[TDX_Service] ADD CONSTRAINT DF_DEFAULT_TDX_Service_OverAge DEFAULT 0 FOR [OverAge];
ALTER TABLE [dbo].[TDX_Service] ADD CONSTRAINT DF_DEFAULT_TDX_Service_SequenceNo DEFAULT 0 FOR [SequenceNo];

ALTER TABLE [dbo].[TDX_Service] ADD CONSTRAINT DF_DEFAULT_TDX_Service_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_Service] ADD CONSTRAINT DF_DEFAULT_TDX_Service_Updated DEFAULT SysUtcDateTime() FOR [Updated];


CREATE TABLE [dbo].[TDX_DiscountType](
	[Id] bigint PRIMARY KEY IDENTITY,
	[Name] nvarchar(255),
	[Description] nvarchar(4000),
	[DiscountType] [nvarchar](255) NOT NULL,  --percentage, fixed
	[Amount] money NOT NULL,
	[MaximumAmount] money NOT NULL,

	[Created] [datetime2](7) NOT NULL,
	[Updated] [datetime2](7) NOT NULL,
	[Active] [bit] NOT NULL
)



ALTER TABLE [dbo].[TDX_DiscountType] ADD CONSTRAINT DF_DEFAULT_TDX_DiscountType_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_DiscountType] ADD CONSTRAINT DF_DEFAULT_TDX_DiscountType_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_DiscountType] ADD CONSTRAINT DF_DEFAULT_TDX_DiscountType_Updated DEFAULT SysUtcDateTime() FOR [Updated]


CREATE TABLE [dbo].[TDX_Discount](
	[Id] bigint PRIMARY KEY IDENTITY,
	[Name] nvarchar(255),
	[Description] nvarchar(max),
	[DiscountTypeId]  bigint NOT NULL,
	[AccountId] bigint,
	[PlanId] bigint,
	[SubscriptionId] bigint,
	[CouponCode] nvarchar(255),
	[PlanGrade] nvarchar(255),
	[PlanType] nvarchar(255),
	[ValidFrom] datetime2 NOT NULL,
	[ValidTo] datetime2 NOT NULL,
	[RemainingCouponUseCount] bigint NOT NULL,
	[IsContinue] bit NOT NULL,
	[ContinueCount] int NOT NULL,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_Discount] ADD CONSTRAINT DF_DEFAULT_TDX_Discount_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_Discount] ADD CONSTRAINT DF_DEFAULT_TDX_Discount_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_Discount] ADD CONSTRAINT DF_DEFAULT_TDX_Discount_Updated DEFAULT SysUtcDateTime() FOR [Updated]

CREATE TABLE [dbo].[TDX_Plan](
	[Id] bigint PRIMARY KEY IDENTITY(1,1),
	[Name] nvarchar(255) NOT NULL,
	[Description] nvarchar(max),
	[PlanType] nvarchar(255) NOT NULL,
	[TrialPeriodDays] int NOT NULL,
	[IsPromotional] bit NOT NULL,
	[IsEnterprise] bit NOT NULL,
	[IsAddOn] bit NOT NULL,
	[IsInternalPlan] bit NOT NULL,
	[IsCommunication] bit NOT NULL,
	[Quantity] bigint NOT NULL,

	[SequenceNo] int NOT NULL,
	[IsFavourite] bit NOT NULL,
	[IsTrial] bit NOT NULL,

	[PlanGrade] nvarchar(255),
	[Primary] bit NOT NULL, 
	[AlwaysRenew] bit NOT NULL,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL,

	[ReportAccountCodingId] int
)


ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsTrial DEFAULT 0 FOR [IsTrial];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_Primary DEFAULT 0 FOR [Primary];

ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_Updated DEFAULT SysUtcDateTime() FOR [Updated]

ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_TrialPeriodDays DEFAULT 0 FOR [TrialPeriodDays];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsPromotional DEFAULT 0 FOR [IsPromotional];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsEnterprise DEFAULT 0 FOR [IsEnterprise];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsAddOn DEFAULT 0 FOR [IsAddOn];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsInternalPlan DEFAULT 0 FOR [IsInternalPlan];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsCommunication DEFAULT 0 FOR [IsCommunication];

ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_SequenceNo DEFAULT 0 FOR [SequenceNo];
ALTER TABLE [dbo].[TDX_Plan] ADD CONSTRAINT DF_DEFAULT_TDX_Plan_IsFavourite DEFAULT 0 FOR [IsFavourite];



CREATE TABLE [dbo].[TDX_PlanLine](
	[Id] bigint PRIMARY KEY IDENTITY,
	[PlanId] bigint NOT NULL,
	[ServiceId] bigint NOT NULL,
	[Quantity] bigint NOT NULL,
	[IsUnlimited] bit NOT NULL,
	[Active] bit NOT NULL,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL
	
)

ALTER TABLE [dbo].[TDX_PlanLine] ADD CONSTRAINT DF_DEFAULT_TDX_PlanLine_IsUnlimited DEFAULT 0 FOR [IsUnlimited];
ALTER TABLE [dbo].[TDX_PlanLine] ADD CONSTRAINT DF_DEFAULT_TDX_PlanLine_Quantity DEFAULT 0 FOR [Quantity];
ALTER TABLE [dbo].[TDX_PlanLine] ADD CONSTRAINT DF_DEFAULT_TDX_PlanLine_Active DEFAULT 1 FOR [Active];

ALTER TABLE [dbo].[TDX_PlanLine] ADD CONSTRAINT DF_DEFAULT_TDX_PlanLine_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_PlanLine] ADD CONSTRAINT DF_DEFAULT_TDX_PlanLine_Updated DEFAULT SysUtcDateTime() FOR [Updated];


CREATE TABLE [dbo].[TDX_Order](
	[Id] bigint PRIMARY KEY IDENTITY,
	[AccountId] bigint NOT NULL,
	[Amount] money,
	[Status] varchar(128), -- completed, draft, 
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_Order] ADD CONSTRAINT DF_DEFAULT_TDX_Order_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_Order] ADD CONSTRAINT DF_DEFAULT_TDX_Order_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_Order] ADD CONSTRAINT DF_DEFAULT_TDX_Order_Updated DEFAULT SysUtcDateTime() FOR [Updated]




CREATE TABLE [dbo].[TDX_OrderLine](
	[Id] bigint PRIMARY KEY IDENTITY,
	[OrderId] bigint NOT NULL,
	[PlanId] bigint NOT NULL,
	[SubscriptionId] bigint NOT NULL,
	[Amount] money,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_OrderLine] ADD CONSTRAINT DF_DEFAULT_TDX_OrderLine_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_OrderLine] ADD CONSTRAINT DF_DEFAULT_TDX_OrderLine_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_OrderLine] ADD CONSTRAINT DF_DEFAULT_TDX_OrderLine_Updated DEFAULT SysUtcDateTime() FOR [Updated]


CREATE TABLE [dbo].[TDX_Invoice](
	[Id] bigint PRIMARY KEY IDENTITY,	
	[AccountId] bigint NOT NULL,
	[OrderId] bigint NOT NULL,

	--stripe field
	[StripeCustomerId] varchar(255), 
	[StripeChargeId] varchar(255), 
	[StripeSubscriptionId] varchar(255), 
	[StripeInvoiceId] varchar(255), 
	--stripe field	

	[Amount] money NOT NULL,
	[CurrencyCode] nvarchar(255) NOT NULL,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL,

	[BillingAddress] varchar(255), 
	[BillingState] varchar(255), 
	[BillingCountry] varchar(255), 
	[BillingPostalCode] varchar(255), 
	[Industry] varchar(255)
)



ALTER TABLE [dbo].[TDX_Invoice] ADD CONSTRAINT DF_DEFAULT_TDX_Invoice_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_Invoice] ADD CONSTRAINT DF_DEFAULT_TDX_Invoice_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_Invoice] ADD CONSTRAINT DF_DEFAULT_TDX_Invoice_Updated DEFAULT SysUtcDateTime() FOR [Updated]

CREATE TABLE [dbo].[TDX_InvoiceLine](
	[Id] bigint PRIMARY KEY IDENTITY,
	[InvoiceId] bigint NOT NULL,	
	[OrderLineId] bigint NOT NULL,
	[DiscountId] bigint NOT NULL,
	[Amount] money NOT NULL,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_InvoiceLine] ADD CONSTRAINT DF_DEFAULT_TDX_InvoiceLine_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_InvoiceLine] ADD CONSTRAINT DF_DEFAULT_TDX_InvoiceLine_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_InvoiceLine] ADD CONSTRAINT DF_DEFAULT_TDX_InvoiceLine_Updated DEFAULT SysUtcDateTime() FOR [Updated]
ALTER TABLE [dbo].[TDX_InvoiceLine] ADD CONSTRAINT DF_DEFAULT_TDX_InvoiceLine_DiscountId DEFAULT 0 FOR [DiscountId]



CREATE TABLE [dbo].[TDX_Payment](
	[Id] bigint PRIMARY KEY IDENTITY,
	[InvoiceId] bigint NOT NULL,
	[Amount] money NOT NULL,
	[Channel] varchar(255),

	--stripe field
	[StripeChargeId] varchar(255),
	[StripeCustomerId] varchar(255),
	[StripeBalanceTransactionId] varchar(255),
	[Description] varchar(255),
	[StripeInvoiceId] varchar(255),

	 [NetTransactionAmount] money,
	 [TransactionFees] money,

	 [IsPaid] bit NOT NULL,
	-- end

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)



ALTER TABLE [dbo].[TDX_Payment] ADD CONSTRAINT DF_DEFAULT_TDX_Payment_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_Payment] ADD CONSTRAINT DF_DEFAULT_TDX_Payment_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_Payment] ADD CONSTRAINT DF_DEFAULT_TDX_Payment_Updated DEFAULT SysUtcDateTime() FOR [Updated]


CREATE TABLE [dbo].[TDX_PaymentSetting](
	[Id] [bigint] PRIMARY KEY IDENTITY,
	[AccountId] [bigint] NOT NULL,
	[Industry] [nvarchar](255) NULL,
	[BillingAddress] [nvarchar](255) NULL,
	[BillingState] [nvarchar](255) NULL,
	[BillingPostalCode] [nvarchar](255) NULL,
	[BillingCountry] [nvarchar](255) NULL,
	[PaymentMethod] [nvarchar](255) NULL,
	[PaymentCustomerId] [nvarchar](255) NULL,
	[CurrencyCode] nvarchar(5),
	

	[CardCountryOrigin] nvarchar(5) NULL,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_PaymentSetting] ADD CONSTRAINT DF_DEFAULT_TDX_PaymentSetting_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_PaymentSetting] ADD CONSTRAINT DF_DEFAULT_TDX_PaymentSetting_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_PaymentSetting] ADD CONSTRAINT DF_DEFAULT_TDX_PaymentSetting_Updated DEFAULT SysUtcDateTime() FOR [Updated]



CREATE TABLE [dbo].[TDX_Subscription](
	[Id] bigint PRIMARY KEY IDENTITY,	
	[PlanId] bigint NOT NULL,
	[AccountId] bigint NOT NULL,
	[StartDate] datetime2 NOT NULL,
	[ExpiryDate] datetime2 NOT NULL,
	[IsRecurring] bit NOT NULL,
	[PaymentInterval] varchar(255),
	[PaymentIntervalCount] int NOT NULL,
	[PriceId] bigint NOT NULL,	
	[RenewAttemptCount] int NOT NULL,	
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_Subscription] ADD CONSTRAINT DF_DEFAULT_TDX_Subscription_Active DEFAULT 0 FOR [Active]
ALTER TABLE [dbo].[TDX_Subscription] ADD CONSTRAINT DF_DEFAULT_TDX_Subscription_IsRecurring DEFAULT 0 FOR [IsRecurring]
ALTER TABLE [dbo].[TDX_Subscription] ADD CONSTRAINT DF_DEFAULT_TDX_Subscription_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_Subscription] ADD CONSTRAINT DF_DEFAULT_TDX_Subscription_Updated DEFAULT SysUtcDateTime() FOR [Updated]


CREATE TABLE [dbo].[TDX_SubscriptionLine](
	[Id] bigint PRIMARY KEY IDENTITY,	
	[SubscriptionId] bigint NOT NULL,	
	[StartDate] datetime2 NOT NULL,
	[EndDate] datetime2 NOT NULL,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)

ALTER TABLE [dbo].[TDX_SubscriptionLine] ADD CONSTRAINT DF_DEFAULT_TDX_SubscriptionLine_Active DEFAULT 1 FOR [Active]
ALTER TABLE [dbo].[TDX_SubscriptionLine] ADD CONSTRAINT DF_DEFAULT_TDX_SubscriptionLine_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_SubscriptionLine] ADD CONSTRAINT DF_DEFAULT_TDX_SubscriptionLine_Updated DEFAULT SysUtcDateTime() FOR [Updated]


CREATE TABLE [dbo].[TDX_UsageHistory](
	[Id] bigint PRIMARY KEY IDENTITY,
	[AccountId] bigint NOT NULL,
	[SubscriptionId] bigint,
	[SubscriptionLineId] bigint,
	[PlanId] bigint,
	[ServiceId] bigint,
	[ServiceCode] nvarchar(255),
	
	[Quantity] bigint NOT NULL,
	
	[ApiUserId] bigint,
	[Description] nvarchar(500),
	[IsCommunication] bit NOT NULL,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL
)


ALTER TABLE [dbo].[TDX_UsageHistory] ADD CONSTRAINT DF_DEFAULT_TDX_UsageHistory_IsCommunication DEFAULT 0 FOR [IsCommunication];

ALTER TABLE [dbo].[TDX_UsageHistory] ADD CONSTRAINT DF_DEFAULT_TDX_UsageHistory_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_UsageHistory] ADD CONSTRAINT DF_DEFAULT_TDX_UsageHistory_Updated DEFAULT SysUtcDateTime() FOR [Updated];

CREATE TABLE [dbo].[TDX_PreSignup](
	[Id] bigint PRIMARY KEY IDENTITY,
	[Username] nvarchar(255) NOT NULL,
	[AccountId] bigint NOT NULL,
	[PlanId] bigint NOT NULL,
	
	[PaymentChannel] nvarchar(255),
	[PaymentToken] nvarchar(255),
	[CurrencyCode] nvarchar(5),
	
    [ActivationLinkExpiry] datetime2 NOT NULL,

	[ActivationAttempts] int NOT NULL,
	[CouponCode] nvarchar(128),
	[Address] nvarchar(512),
	[PostCode] nvarchar(10),
	[State] nvarchar(50),
	[ISOCountryCode] nvarchar(50),
	[Industry] nvarchar(128),
	[TimeZoneId] nvarchar(128),
	[DisplayedPrice] money NOT NULL,
	
	[IsRecurring] bit NOT NULL,
	[IsActivated] bit NOT NULL,

	[EmailAddress] nvarchar(255),

	[PrevECommereceUsed] nvarchar(100),
	[PrevMarketingPlatformUsed] nvarchar(100),
	[CurrentEmailListSize] nvarchar(100),
	[IsDemoRequested] bit NOT NULL DEFAULT 0,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)



ALTER TABLE [dbo].[TDX_PreSignup] ADD CONSTRAINT DF_DEFAULT_TDX_PreSignup_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_PreSignup] ADD CONSTRAINT DF_DEFAULT_TDX_PreSignup_Updated DEFAULT SysUtcDateTime() FOR [Updated]

CREATE TABLE [dbo].[TDX_Currency](
	[Id] bigint PRIMARY KEY IDENTITY,
	[Code] nvarchar(255) NOT NULL,
	[Country] nvarchar(50),
	[Number] int NOT NULL,
	[Name] nvarchar(255),
	[Symbol] nvarchar(10)
)


CREATE TABLE [dbo].[TDX_Price](
	[Id] bigint PRIMARY KEY IDENTITY,
	[PlanId] bigint NOT NULL,
	[Price] money NOT NULL,
	[CurrencyCode] nvarchar(255) NOT NULL,
	[BasePrice] money NOT NULL,
	[Offset] money NOT NULL,
	
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL,
)


ALTER TABLE [dbo].[TDX_Price] ADD CONSTRAINT DF_DEFAULT_TDX_Price_Active DEFAULT 1 FOR [Active];
ALTER TABLE [dbo].[TDX_Price] ADD CONSTRAINT DF_DEFAULT_TDX_Price_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_Price] ADD CONSTRAINT DF_DEFAULT_TDX_Price_Updated DEFAULT SysUtcDateTime() FOR [Updated];


CREATE TABLE [dbo].[TDX_CustomService](
	[Id] bigint PRIMARY KEY IDENTITY,
	[ServiceId] bigint NOT NULL,
	[AccountId] bigint NOT NULL,
	[Name] nvarchar(255) NOT NULL,
	[Description] nvarchar(max),	

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL
)


ALTER TABLE [dbo].[TDX_CustomService] ADD CONSTRAINT DF_DEFAULT_TDX_CustomService_Created DEFAULT SysUtcDateTime() FOR [Created]
ALTER TABLE [dbo].[TDX_CustomService] ADD CONSTRAINT DF_DEFAULT_TDX_CustomService_Updated DEFAULT SysUtcDateTime() FOR [Updated]

CREATE TABLE [dbo].[TDX_Notification](
	[Id] bigint PRIMARY KEY IDENTITY,
	[SubscriptionId] bigint,
	[InvoiceId] bigint,
	[AccountId] bigint,	
	
	[Type] nvarchar(50),

	[SendBefore] int NOT NULL,
	[Sent] bit NOT NULL,
	[Quantity] money NOT NULL,

	[ServiceType] nvarchar(255),

	[Description] nvarchar(max),
	[System] bit NOT NULL,
	[Read] bit NOT NULL,

	[SentDate] datetime2,
	[ReadDate] datetime2,

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL,
)


ALTER TABLE [dbo].[TDX_Notification] ADD CONSTRAINT DF_DEFAULT_TDX_Notification_Active DEFAULT 1 FOR [Active];
ALTER TABLE [dbo].[TDX_Notification] ADD CONSTRAINT DF_DEFAULT_TDX_Notification_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_Notification] ADD CONSTRAINT DF_DEFAULT_TDX_Notification_Updated DEFAULT SysUtcDateTime() FOR [Updated];


CREATE TABLE [dbo].[TDX_CostPerIndex](
	[Id] bigint PRIMARY KEY IDENTITY,
	[Type] nvarchar(50),  -- percentage or fixed
	[Amount] money NOT NULL, 
	[ApplyDate] datetime2 NOT NULL, -- order by apply date and apply to get the current price
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
	
)


ALTER TABLE [dbo].[TDX_CostPerIndex] ADD CONSTRAINT DF_DEFAULT_TDX_CostPerIndex_Active DEFAULT 1 FOR [Active];
ALTER TABLE [dbo].[TDX_CostPerIndex] ADD CONSTRAINT DF_DEFAULT_TDX_CostPerIndex_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_CostPerIndex] ADD CONSTRAINT DF_DEFAULT_TDX_CostPerIndex_Updated DEFAULT SysUtcDateTime() FOR [Updated];


CREATE TABLE [dbo].[TDX_SubscriptionUpdate](
	[Id] bigint PRIMARY KEY IDENTITY,
	[AccountId] bigint NOT NULL,  
	[FromSubscriptionId] bigint NOT NULL,  
	[ToPlanId] bigint NOT NULL,  
	[ToPriceId] bigint NOT NULL,  
	[IsRecurring] bit NOT NULL,  
	[ForceUpgrade] bit NOT NULL, 	

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL	
)

ALTER TABLE [dbo].[TDX_SubscriptionUpdate] ADD CONSTRAINT DF_DEFAULT_TDX_SubscriptionUpdate_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_SubscriptionUpdate] ADD CONSTRAINT DF_DEFAULT_TDX_SubscriptionUpdate_Updated DEFAULT SysUtcDateTime() FOR [Updated];
ALTER TABLE [dbo].[TDX_SubscriptionUpdate] ADD CONSTRAINT DF_DEFAULT_TDX_SubscriptionUpdate_Active DEFAULT 1 FOR [Active];


CREATE TABLE [dbo].[TDX_PlanDefinition](
	[Id] bigint PRIMARY KEY IDENTITY,
	[PlanId] bigint NOT NULL,
	[Description] nvarchar(4000),
	[SequenceNo] int NOT NULL,
	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL,
	[Active] bit NOT NULL
)


ALTER TABLE [dbo].[TDX_PlanDefinition] ADD CONSTRAINT DF_DEFAULT_TDX_PlanDefinition_SequenceNo DEFAULT 0 FOR [SequenceNo];
ALTER TABLE [dbo].[TDX_PlanDefinition] ADD CONSTRAINT DF_DEFAULT_TDX_PlanDefinition_Active DEFAULT 1 FOR [Active];
ALTER TABLE [dbo].[TDX_PlanDefinition] ADD CONSTRAINT DF_DEFAULT_TDX_PlanDefinition_Created DEFAULT SysUtcDateTime() FOR [Created];
ALTER TABLE [dbo].[TDX_PlanDefinition] ADD CONSTRAINT DF_DEFAULT_TDX_PlanDefinition_Updated DEFAULT SysUtcDateTime() FOR [Updated];



CREATE TABLE [dbo].[TDX_ReportAccountCoding](
		
	[Id] int PRIMARY KEY IDENTITY,	
	[ReportAccountCode] nvarchar(255),
	[ReportAccountName] nvarchar(max)
)


CREATE TABLE TDX_SpecialOfferLink
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	PlanId BIGINT null,
	ValidFrom datetime2 NOT NULL,
	ValidTo datetime2 NOT NULL,
	Name NVARCHAR(255) NULL,
	Token NVARCHAR(255) NOT NULL,
	ContactCount bigint NOT NULL,
	Active bit NOT NULL
)

ALTER TABLE [dbo].[TDX_SpecialOfferLink] ADD CONSTRAINT DF_DEFAULT_TDX_SpecialOfferLink_Active DEFAULT 1 FOR [Active];




/* Swipe n go tables end */


CREATE TABLE [dbo].[TDX_APICallLimitSetting](
	[Id] bigint PRIMARY KEY IDENTITY,
	[AccountId] bigint,
	[Global] bit NOT NULL,
	[Name] nvarchar(250),
	[RateLimitForPeriod] bigint NOT NULL,
	[RateLimitCount] bigint NOT NULL,
)


/* Facebook autogenerated userid data type is numeric string and therefore defined as VARCHAR instead of NVARCHAR*/
CREATE TABLE TDX_FacebookAccountSetting
(
Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
AccountId BIGINT NOT NULL,
UserId VARCHAR(255) NOT NULL,
UserAccessToken NVARCHAR(4000) NOT NULL,
ExpiresOn DATETIME2 NULL,
CreatedOn DATETIME2 NOT NULL,
LastModifiedOn DATETIME2 NOT NULL
)

/* Facebook autogenerated pageid data type is numeric string and therefore defined as VARCHAR instead of NVARCHAR*/
CREATE TABLE TDX_FacebookPageSetting
(
Id BIGINT NOT NULL IDENTITY(1,1) PRIMARY KEY,
AccountId BIGINT NOT NULL,
PageId VARCHAR(255) NOT NULL,
FormId BIGINT NOT NULL,
TabText NVARCHAR(255),
CreatedOn DATETIME2 NOT NULL,
LastModifiedOn DATETIME2 NOT NULL
)


CREATE TABLE TDX_FacebookAutoPostSetting
(
	[Id] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,	
	[Message] nvarchar(255) NOT NULL,
	[SocialCardTitle] nvarchar(255),
	[SocialCardDescription] nvarchar(500),
	[SocialCardImageURL] nvarchar(max),	
	[PostOnProfile] bit,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,
	[Enabled] BIT NOT NULL,
	[SocialCardEnabled] BIT NOT NULL,
	)

  
  /* Facebook auto generated pageid data type is numeric string and Since this id is also part of composite key, its defined as VARCHAR instead of NVARCHAR  */
 CREATE TABLE TDX_FacebookAutoPostPage
  (
    FacebookAutoPostSettingId BIGINT NOT NULL,
	PageId VARCHAR(255) NOT NULL,
	PRIMARY KEY(FacebookAutoPostSettingId, PageId)
  )


  /* Lead Score tables start */

  CREATE TABLE TDX_LeadScoreConfig
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,

	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL,
	Active bit NOT NULL
)


ALTER TABLE [TDX_LeadScoreConfig] ADD CONSTRAINT
            unique_account_leadscore_config UNIQUE NONCLUSTERED
    (
                [AccountId]
    )


CREATE TABLE TDX_LeadScore
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	Name nvarchar(100),

	MinValue int NOT NULL,
	MaxValue int NOT NULL,
	
	InitialValue int NOT NULL,
	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL,
	Active bit NOT NULL
)


CREATE TABLE TDX_LeadScoreAction
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	LeadScoreId bigint NOT NULL,
	[Action] nvarchar(100),
	Value int NOT NULL,
	ExpiresIn bigint NOT NULL,
	[Name] nvarchar(100),
	[SegmentId] bigint,
	IsManual bit NOT NULL,
	[CustomInteraction] [nvarchar](50) NULL,
	IsOverridable bit NOT NULL,
	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL,
	Active bit NOT NULL
)


ALTER TABLE [dbo].[TDX_LeadScoreAction] ADD CONSTRAINT DF_DEFAULT_TDX_LeadScoreAction_IsManual DEFAULT 0 FOR [IsManual];


CREATE TABLE TDX_LeadScoreActionOverride
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	LeadScoreActionId BIGINT NOT NULL,

	CampaignId bigint,
	FormId bigint,
	TriggeredSendId bigint,
	OverrideValue int,
	
	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL,
	Active bit NOT NULL
)



CREATE TABLE TDX_LeadScoreContact
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	ContactId BIGINT NOT NULL,
	LeadScoreId bigint NOT NULL,
	Value int NOT NULL,
	PreviousValue int NOT NULL,
	
	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL,
	Active bit NOT NULL
)


CREATE TABLE TDX_LeadScoreEventProcessTracker
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	LastProcessedId BIGINT NOT NULL,
	LeadScoreActionId BIGINT,
	[Type] nvarchar(100),
	
	ProcessedDate DateTime2 NOT NULL
)



CREATE TABLE TDX_LeadScoreEvent
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	LeadScoreActionId BIGINT NOT NULL,
	LeadScoreId BIGINT NOT NULL,
	IsProcessed bit NOT NULL,
	ContactId BIGINT,

	EmailOpenId BIGINT,
	EmailLinkClickId BIGINT,
	EmailUnsubscribeId BIGINT,
	FormSubmitId BIGINT,
	CustomInteractionId BIGINT,

	LeadScoreActionOverrideId BIGINT,
	EmailDesignId BIGINT NULL,
	EmailLinkId bigint NULL
	
)






CREATE TABLE TDX_LeadScoreInteraction
(
	Id BIGINT NOT NULL IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	ContactId bigint NOT NULL,
	LeadScoreEventId bigint,
	LeadScoreActionId BIGINT NOT NULL,
	LeadScoreId BIGINT NOT NULL,
	
	ValueChange int NOT NULL,
	ProcessingOrder int NOT NULL,

	IsReverseRecord bit NOT NULL,
	IsReversed bit NOT NULL,
	IsManual bit NOT NULL,

	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL
	
)

ALTER TABLE [dbo].[TDX_LeadScoreInteraction] ADD CONSTRAINT DF_DEFAULT_TDX_LeadScoreInteraction_IsManual DEFAULT 0 FOR [IsManual];



CREATE TABLE TDX_LeadScoreThreshold
(
	Id BIGINT not null IDENTITY(1,1) PRIMARY KEY,
	AccountId BIGINT NOT NULL,
	LeadScoreId bigint NOT NULL,

	ValueFrom int NOT NULL,
	ValueTo int NOT NULL,

	Name nvarchar(100),
	SequenceNo int NOT NULL,
	[Description] nvarchar(4000),

	Created DateTime2 NOT NULL,
	Updated DateTime2 NOT NULL,
	Active bit NOT NULL
)



ALTER TABLE [TDX_LeadScoreThreshold]
ADD CONSTRAINT range_check CHECK ([ValueFrom] <= ValueTo);


/* Lead Score tables end */

/* ECommerce Tables Begin*/
CREATE TABLE [dbo].[TDX_ECommerceAccountSetting](
	[Id] [bigint] IDENTITY(1,1)  PRIMARY KEY,
	[ECommerceAccountId] [bigint] NOT NULL,
	[UserInstanceId] [bigint] NOT NULL,
	[FileProfileId] [bigint] NOT NULL,
	[ShopURL] [nvarchar](max) NOT NULL,
	[Credentials] [nvarchar](max) NOT NULL,	
	[CustomersAcceptedEmails] [bit] NOT NULL,
	[TriggerSendId] [bigint] NOT NULL,
	[SendAbandonedCartEmail] [bit] NOT NULL,
	[ExpiresOn] [datetime2](7) NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,
	[ProductImport] [bit] NOT NULL,
	[OrderImport] [bit] NOT NULL,
	[ContactImport] [bit] NOT NULL
)


ALTER TABLE [dbo].[TDX_ECommerceAccountSetting] ADD  CONSTRAINT [DF_TDX_ECommerceAccountSetting_CustomersAcceptedEmails]  DEFAULT ((0)) FOR [CustomersAcceptedEmails]

ALTER TABLE [dbo].[TDX_ECommerceAccountSetting] ADD  CONSTRAINT [DF_TDX_ECommerceAccountSetting_SendAbandonedCartEmail]  DEFAULT ((0)) FOR [SendAbandonedCartEmail]


CREATE TABLE [dbo].[TDX_ECommerceAccountConfig](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[AccountId] [bigint] NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,
	[Active] [bit] NOT NULL
)


CREATE TABLE [dbo].[TDX_ECommerceEntity](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[Name] [varchar](50) NOT NULL,
	[Configuration] [nvarchar](MAX) NOT NULL
)


CREATE TABLE [dbo].[TDX_ECommerceAccount](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[ECommerceEntityId] [bigint] NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Enabled] [bit] NOT NULL
)


CREATE TABLE [dbo].[TDX_ECommerceStoreProduct](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[StoreProductId] [nvarchar](100) NOT NULL,
	[ECommerceAccountId] [bigint] NOT NULL,
	[Title] [nvarchar](100) NOT NULL,
	[ProductType] [nvarchar](100) NULL,
	[Tags] [nvarchar](max) NULL,
	[Path] [nvarchar](max) NULL,
	[ImageUrl] [nvarchar](max) NULL,
	[Description] [nvarchar](max) NULL,
	[Vendor] [nvarchar](100) NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL
)


CREATE TABLE [dbo].[TDX_ECommerceStoreProductVariant](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY NOT NULL,
	[StoreVariantId] [nvarchar](100) NOT NULL,
	[ProductId] [bigint] NOT NULL,
	[Title] [nvarchar](100) NOT NULL,
	[Price] [numeric](10, 4) NULL,
	[Sku] [nvarchar](100) NULL,
	[Barcode] [nvarchar](100) NULL,
	[Weight] [numeric](10, 4) NULL,
	[WeightUnit] [nvarchar](10) NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL
)


CREATE TABLE [dbo].[TDX_ECommerceStoreOrder](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[StoreOrderId] [nvarchar](100) NOT NULL,
	[ContactId] [bigint] NOT NULL,
	[ReferringSite] [nvarchar](max) NULL,
	[LandingSiteRef] [nvarchar](max) NULL,
	[Type] [int] NULL,
	[Tags] [nvarchar](max) NULL,
	[OrderCreatedOn] [datetime2](7) NOT NULL,
	[OrderUpdatedOn] [datetime2](7) NOT NULL,
	[OrderProcessedOn] [datetime2](7) NULL,
	[Confirmed] [bit] NOT NULL,
	[TotalPrice] [numeric](10, 4) NULL,
	[Currency] [nvarchar](10) NULL,
	[FinancialStatus] [nvarchar](100) NULL,
	[FulfillmentStatus] [nvarchar](100) NULL,
	[AbandonedCheckoutUrl] [nvarchar](max) NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL
)


ALTER TABLE [dbo].[TDX_ECommerceStoreOrder] ADD  CONSTRAINT [DF_TDX_ECommerceStoreOrder_Type]  DEFAULT ((0)) FOR [Type]

ALTER TABLE [dbo].[TDX_ECommerceStoreOrder] ADD  CONSTRAINT [DF_TDX_ECommerceStoreOrder_Confirmed]  DEFAULT ((1)) FOR [Confirmed]


CREATE TABLE [dbo].[TDX_ECommerceStoreOrderItem](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[StoreItemId] [nvarchar](100) NOT NULL,
	[OrderId] [bigint] NOT NULL,
	[VariantId] [bigint] NOT NULL,
	[Quantity] [int] NOT NULL,
	[Price] [numeric](10, 4) NOT NULL,
	[TotalDiscount] [numeric](10, 4) NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL
)

CREATE TABLE [dbo].[TDX_ECommerceProdRecJob](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[Name] [varchar](50) NOT NULL,
	[ECommerceProdRecModelId] [bigint] NOT NULL,
	[NoOfRecommendations] [int] NOT NULL,
	[CriteriaId] [bigint] NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[CreatedBy] [bigint] NOT NULL,
	[UpdatedOn] [datetime2](7) NOT NULL,
	[UpdatedBy] [bigint] NOT NULL,
	[NextRunScheduledOn] datetime2(7),
	[ScheduleTypeId] int NOT NULL,
	[DaysOfWeek] varchar(7),
	[DaysOfMonth] varchar(31),
	[ScheduleStartingFrom] datetime2(7),
	[ScheduleEndingOn] datetime2(7)
)



CREATE TABLE [dbo].[TDX_ECommerceProdRecJobRun]
(
	Id bigint PRIMARY KEY IDENTITY(1,1),
	ProdRecJobId bigint NOT NULL,
	Status int NOT NULL,
	StartSendDateTime datetime2(7),
	FinishSendDateTime datetime2(7),
	NoOfContacts bigint NOT NULL
)



ALTER TABLE [dbo].[TDX_ECommerceProdRecJobRun] ADD  CONSTRAINT [DF_TDX_ECommerceProdRecJobRun_Status]  DEFAULT ((0)) FOR [Status]


ALTER TABLE [dbo].[TDX_ECommerceProdRecJobRun] ADD  CONSTRAINT [DF_TDX_ECommerceProdRecJobRun_NoOfContacts]  DEFAULT ((0)) FOR [NoOfContacts]


CREATE TABLE [dbo].[TDX_ECommerceProdRecJobContacts](
	[Id] [bigint] PRIMARY KEY,
	[ContactId] [bigint] NOT NULL,
	[ProdRecJobRunId] [bigint] NOT NULL,
	[State] [tinyint] NOT NULL,
	[Error] [tinyint] NULL,
	[Pause] [bit] NOT NULL,
	[Priority] [tinyint] NOT NULL,
	[Worker] [uniqueidentifier] NULL,
	[Chunk] [bigint] NULL
)

CREATE TABLE [dbo].[TDX_ECommerceProdRecModel](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[AzureId] [varchar](50) NOT NULL,
	[Name] [varchar](50) NOT NULL,
	[Description] [varchar](50) NOT NULL,
	[ECommerceAccountId] [bigint] NOT NULL,
	[ActiveBuildId] [bigint] NOT NULL,
	[IsBuilt] [bit] NOT NULL,
	[MonthlyLimit] [bigint] NOT NULL,
	[MonthlyLimitUpdatedOn] datetime2(7) NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[CreatedBy] [bigint] NOT NULL,
	[UpdatedOn] [datetime2](7) NOT NULL,
	[UpdatedBy] [bigint] NOT NULL
)

CREATE TABLE [dbo].[TDX_ECommerceProdRecResult](
	[Id] [bigint] PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[ProdRecJobRunId] [bigint] NOT NULL,
	[ContactId] [bigint] NOT NULL,
	[ProductId] [bigint] NULL,
	[Rating] [decimal](6, 4) NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[CreatedBy] [bigint] NOT NULL,
	[UpdatedOn] [datetime2](7) NOT NULL,
	[UpdatedBy] [bigint] NOT NULL
)

IF EXISTS(SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProdRecJobContactsSequence]') AND type = 'SO')
drop SEQUENCE ProdRecJobContactsSequence



CREATE SEQUENCE [dbo].[ProdRecJobContactsSequence] 
 AS [bigint]
 START WITH 1
 INCREMENT BY 1
 MINVALUE -9223372036854775808
 MAXVALUE 9223372036854775807
 CACHE  50 


/* ECommerce Tables End */

create table TDX_CompetitionItem (
	Id bigint PRIMARY KEY IDENTITY,
	CompetitionInstanceId bigint NOT NULL,
	ItemType varchar(50) NOT NULL,
	ItemId bigint NOT NULL,
	ItemPurpose varchar(50) NOT NULL,
	CompetitionOutcomeMapping nvarchar(50) NULL
)



-- An item can't be bound to more than one competition, or to one competition multiple times.
alter table TDX_CompetitionItem
add constraint UNQ_TDX_CompetitionItem_ItemType_ItemId
unique (ItemType, ItemId)



create table TDX_CompetitionFormField (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	CompetitionFieldName nvarchar(255) NOT NULL,
	FormFieldName nvarchar(255) NULL,
	FormId bigint NOT NULL
)



alter table TDX_CompetitionFormField
add constraint UNQ_TDX_CompetitionFormField_FormId_CompetitionFieldName
unique (FormId, CompetitionFieldName)




create table TDX_CompetitionAction (
	Id bigint PRIMARY KEY IDENTITY,
	CompetitionInstanceId bigint NOT NULL,
	TransactionId bigint NOT NULL,
	ContactId bigint NULL,
	Date_UTC datetime2 NOT NULL,
	CompetitionAction nvarchar(10) NOT NULL
)



CREATE TABLE [dbo].[TDX_EmailAddress](
	[Id] [bigint] PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[EmailAddress] [nvarchar](255) NOT NULL,
	[Subscribed] [bit] NOT NULL,
	[AccountId] [bigint] NULL
)



alter table TDX_EmailAddress
add constraint UNQ_TDX_EmailAddress_EmailAddress
unique (EmailAddress)



CREATE TABLE [dbo].[TDX_MobileWalletProject](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ProjectName] [nvarchar](50) NOT NULL,
	[PassStyleType] [nvarchar](50) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Status] [nvarchar](200) NOT NULL,
	[PassTemplateXML] [nvarchar](max) NOT NULL,
	[PassTypeIdentifier] [nvarchar](200) NOT NULL,
	[BarCodeFormat] [nvarchar](200) NOT NULL,
	[BarcodeValue] nvarchar(500) NOT NULL,
	[BarcodeAlternateTextField] nvarchar(500),
	[ExpirationDate] datetime2 NULL,
	[S2apType] [nvarchar](50) NOT NULL,
	[S2apClassId] [nvarchar](200) NOT NULL,
	[S2apServiceAccountId] [nvarchar](200) NOT NULL,
	[S2apStatus] [nvarchar](200) NOT NULL,
	[S2apValidTimeIntervalStart] datetime2 NULL,
	[S2apValidTimeIntervalEnd] datetime2 NULL,
	[S2apClassXML] nvarchar(MAX),
	[CreatedOn] [datetime] NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,	
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]



CREATE TABLE [dbo].[TDX_MobileWalletRegistration](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[ContactId] [bigint] NOT NULL,
	[MobileWalletProjectId] [bigint] NOT NULL,
	[PassTypeIdentifier] [nvarchar](200) NOT NULL,
	[SerialNumber] [nvarchar](200) NOT NULL,
	[PushToken] [nvarchar](200) NOT NULL,
	[DeviceLibraryId] [nvarchar](200) NOT NULL,
	[RegisteredByTransactionLogId] [bigint] NOT NULL,
	[UnregisteredByTransactionLogId] [bigint] NULL,
	[RegisteredOn] [datetime2](7) NULL,
	[UnregisteredOn] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]




CREATE TABLE [dbo].[TDX_MobileWalletS2apRegistration](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[ContactId] [bigint] NOT NULL,
	[MobileWalletProjectId] [bigint] NOT NULL,
	[S2apClassId] [nvarchar](50) NOT NULL,
	[S2apObjectId] [nvarchar](200) NOT NULL,
	[State] [nvarchar](50) NOT NULL,
	[HasUser] [bit] NOT NULL,
	[RegisteredOn] [datetime2](7) NULL,
	[UnregisteredOn] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]




CREATE TABLE [dbo].[TDX_MobileWalletTransactionLog](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NULL,
	[ContactId] [bigint] NULL,
	[MobileWalletProjectId] [bigint] NULL,
	[SerialNumber] [nvarchar](200) NULL,
	[RequestedAt] [datetime2] NOT NULL,
	[Action] [nvarchar](200) NOT NULL,
	[Succeeded] [bit] NOT NULL,
	[Outcomes] [nvarchar](max) NULL,
	[CampaignId] BIGINT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] 




CREATE TABLE [dbo].[TDX_MobileWalletCertificate](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[PassTypeIdentifier] [nvarchar](200) NOT NULL,
	[AccountId] bigint not null,
	[PassCertificateFileName] VARBINARY(MAX) NOT NULL,
	[PassCertificateFilePassword] [nvarchar](50) NULL,
	[PassCertificateExpiry] [datetime2] NOT NULL,
	[CreatedOn] [datetime2] NOT NULL,
	[DeletedOn] [datetime2] NULL,

PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]




CREATE TABLE [dbo].[TDX_MobileWalletS2apCredential](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[ServiceAccountId] [nvarchar](500) NOT NULL,
	[AccountId] bigint not null,
	[ServiceAccountPrivateKeyFileName] VARBINARY(MAX) NOT NULL,
	[ServiceAccountPrivateKeyFilePassword] [nvarchar](50) NULL,
	[ApplicationName] [nvarchar](50) NOT NULL,
	[IssuerId] [nvarchar](50) NOT NULL,
	[CreatedOn] [datetime2] NOT NULL,
	[DeletedOn] [datetime2] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]




CREATE TABLE [dbo].[TDX_MobileWalletProjectChangeHistory](
	[Id] [bigint] IDENTITY(1,1) NOT NULL,
	[MobileWalletProjectId] [bigint] NOT NULL,
	[PassTypeIdentifier] [nvarchar](200) NOT NULL,
	[ProjectName] [nvarchar](50) NOT NULL,
	[ExpirationDate] datetime2 NULL,
	[BarcodeValue] nvarchar(500) NOT NULL,
	[S2apType] [nvarchar](50) NOT NULL,
	[S2apValidTimeIntervalStart] datetime2 NULL,
	[S2apValidTimeIntervalEnd] datetime2 NULL,
	[CreatedOn] [datetime] NOT NULL,
	[S2apClassId] [nvarchar](200) NOT NULL,
	[PassStyleType] [nvarchar](50) NOT NULL,
	[PassTemplateXML] [nvarchar](max) NOT NULL,
	[S2apClassXML] nvarchar(MAX),
	[BarCodeFormat] [nvarchar](200) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[S2apServiceAccountId] [nvarchar](200) NOT NULL,
	[Version] [int] NOT NULL,
	[BarcodeAlternateTextField] nvarchar(500),
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]



alter table TDX_MobileWalletProjectChangeHistory
add constraint FK_TDX_MobileWalletProjectChangeHistory_MobileWalletProjectId
FOREIGN KEY (MobileWalletProjectId)
references TDX_MobileWalletProject (Id)


CREATE TABLE [dbo].[TDX_ServiceAccountConfig](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[AccountId] [bigint] NOT NULL,
	[ServiceType] [nvarchar](100) NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL,
	[Active] [bit] NOT NULL
)


CREATE TABLE [dbo].[TDX_MobileWalletPassUpdate](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[AccountId] [bigint] NOT NULL,
	[MobileWalletProjectId] [bigint] NOT NULL,	
	[AppleCount] [bigint] NOT NULL,	
	[AndroidCount] [bigint] NOT NULL,	
	[IsProcessed] [bit] NOT NULL,
	[Status] [int] NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL	
)


CREATE TABLE [dbo].[TDX_FormFileUpload](
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[FileName] [nvarchar](255) NULL,
	[GoogleResponse] [nvarchar](max) NULL,
	[ImageContent] [varbinary](max) NULL,
	[BarCodeData] [nvarchar](max) NULL,
	[FormSubmitId]  bigint NOT NULL
)


CREATE TABLE [dbo].[TDX_DomainMonitoringDetails] (
	[Id] [bigint] IDENTITY(1,1) PRIMARY KEY,
	[DomainId] [bigint] NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[DomainCommand] [varchar](50) NOT NULL,
	[DomainMonitoringData] [nvarchar](max) NULL,
	[LastUpdatedDate] [datetime2] NOT NULL
)


CREATE TABLE TDX_SMSIncomingAction
(
	[Id] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[EndpointId] [bigint] NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[ContactIdentifierFieldId] BIGINT NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[Description] [nvarchar](4000) NULL,
	[Action] [nvarchar](255) NOT NULL,
	[ReplyEnabled] [bit] NULL,
	[ValidFrom] DATETIME2 NOT NULL,
	[ValidTo] DATETIME2 NULL,
	[SuccessReplyMessageText] [nvarchar](4000) NULL,
	[FailureReplyMessageText] [nvarchar](4000) NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL
)


CREATE TABLE [dbo].[TDX_SMSEndpoint](
	[Id] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[AggregatorId] [bigint] NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[Name] [nvarchar](255) NOT NULL,
	[Keyword] [nvarchar](255) NULL,
	[ValidFrom] [datetime2](7) NOT NULL,
	[ValidTo] [datetime2](7) NULL,
	-- Mobilenumber column type - VARCHAR is purposely used to avoid UNIQUE constraint max size limit - 900 bytes
	[Mobilenumber] [varchar](255) NOT NULL,
	[SenderName] [nvarchar](255) NULL,
	[ToSend] [bit] NULL,
	[ToReceive] [bit] NULL,
	[ReserveForCompetiton] [bit] not null default(0),
	[CompetitionId] [bigint] NULL,
	[CreatedOn] [datetime2](7) NOT NULL,
	[LastModifiedOn] [datetime2](7) NOT NULL
)


ALTER TABLE TDX_SMSEndpoint
ADD CONSTRAINT UNQ_MobileNumber_Keyword UNIQUE(Mobilenumber, Keyword)



CREATE TABLE [dbo].[TDX_SMSIncomingSubscribe](
	[Id] [bigint] IDENTITY(1,1) NOT NULL PRIMARY KEY,
	[IncomingActionId] [bigint] NOT NULL,
	[ContactListId] [bigint] NOT NULL,
	[CreatedOn] [datetime2](7) NOT NULL
	)



CREATE TABLE TDX_UserAudit
(
	[Id] [bigint] PRIMARY KEY IDENTITY(1,1) NOT NULL,
	[AccountId] [bigint] NOT NULL,
	[UserInstanceId] [bigint] NOT NULL,
	[ActionOccurred_UTC] [datetime2](7) NOT NULL,
	[ActionType] [nvarchar](50) NOT NULL,
	[ActionDetails] [nvarchar](max) NULL
)


CREATE TYPE [dbo].[TDX_TYPE_EmailDesignId] AS TABLE(
	[EmailDesignId] [bigint] NOT NULL
)


/* GDPR Audit Tables Start */

IF OBJECT_ID(N'[dbo].[TDX_GDPRAudit]', 'U') IS NOT NULL
	DROP TABLE [dbo].TDX_GDPRAudit


create table TDX_GDPRAudit(

	Id bigint Primary key IDENTITY(1,1),
	AccountId bigint NOT NULL,
	AuditType varchar(250) NOT NULL,
	Comment varchar(250), -- TODO: This should be unicode

	-- TODO: This is an audit record, it should never be modified. There should not be an updated column
	CreatedBy bigint NOT NULL, -- userUnstanceId
	UpdatedBy bigint NOT NULL, -- userUnstanceId

	Created datetime2 NOT NULL,
	Updated datetime2 NOT NULL
)



/* GDPR Audit Tables End */





create table TDX_GdprRequest (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
	RequestType varchar(50) NOT NULL,
	RequestDate_UTC datetime2 NOT NULL,
	EmailAddress nvarchar(255) NULL,
	MobileNumber nvarchar(50) NULL,
	AlternateKey nvarchar(255) NULL,
	OtherInformation nvarchar(1000) NULL,
	Resolved bit NOT NULL,
	ResolutionComment nvarchar(255) NULL,
	Notes nvarchar(1000) NULL,
	ModifiedBy bigint NULL,
	ModifiedOn datetime2(7) NULL
)



create table TDX_GdprDataProtectionOfficer (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
	UserInstanceId bigint NOT NULL
)



Create Table TDX_CompetitionSMSEntry(
    Id bigint PRIMARY KEY IDENTITY,
	AccountId bigint Not Null,
	CompetitionInstanceId bigint NOT NULL,
	Enabled [bit] NOT NULL,
	ContactUpdateEnabled [bit] NOT NULL,
	TypeOfEntry [int] NOT NULL,
	AgeValidationField  bigint NULL,
	IdentifierField bigint NOT NULL,
	SMSEndpointId  bigint NOT NULL,
	EntryCodeField bigint NULL,
	ParseType [int] Null,
	Age  [int] NULL
)


Create Table TDX_CompetitionKeywordAndContent(
    Id bigint PRIMARY KEY IDENTITY,
	Keyword nvarchar(255) NOT NULL,
	Content nvarchar(2000) NOT NULL,
	CompetitionSMSEntryId bigint NOT NULL
)


 create table TDX_CompetitionSMSField (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	LineNumber [int] NOT NULL,
	ContactFieldId bigint NOT NULL,
	CompetitionSMSEntryId bigint NOT NULL
)


create table TDX_CustomerServiceTeam(

	Id bigint Primary key IDENTITY(1,1),
	AccountId bigint NOT NULL,

	[Name] varchar(250) NOT NULL, 
	[FirstName] varchar(100), 
	[LastName] varchar(100), 
	[EmailAddress] varchar(250), 

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL
)

create table TDX_HandlingHouse(

	Id bigint Primary key IDENTITY(1,1),
	AccountId bigint NOT NULL,

	[Name] varchar(250) NOT NULL, 
	[FirstName] varchar(100), 
	[LastName] varchar(100), 
	[EmailAddress] varchar(250), 

	[Created] datetime2 NOT NULL,
	[Updated] datetime2 NOT NULL
)


create table TDX_AdapterAudit(
	Id bigint IDENTITY(1,1) PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	ApiName varchar(200) NOT NULL,
	TractionEndpointId bigint NOT NULL,
	StartTime datetime2 NOT NULL,
	EndTime datetime2 NULL,
	RequestParameter nvarchar(max) NOT NULL,
	ResponseCode varchar(20) NOT NULL,
	Outcomes nvarchar(max) NULL,
	ErrorMessage nvarchar(max) NULL
)
 

create table TDX_AdapterDynamicAPI(
	Id bigint IDENTITY(1,1) PRIMARY KEY NOT NULL,
	AccountId bigint NOT NULL,
	ApiPath varchar(20) NOT NULL,
	TractionCompanyId bigint NOT NULL, 
	TractionEndpointId bigint NOT NULL,
	TractionFunctionId bigint NOT NULL,
	TractionFunctionName varchar(100) NOT NULL,
	TractionFunctionType varchar(50) NOT NULL,
	TractionFunctionStatus varchar(50) NOT NULL,
	MatchKey varchar(10) NOT NULL, --- 'E','X'
	IdentifireInParam varchar(50) NOT NULL, --- 'customer','customerLookup','both' 
	PreStep varchar(50) NULL,
	UnsubOnFalseSub bit NOT NULL, --- if Subscribe:false, unsub or remain unchanged
	StoreBlankCustomerAttributes bit NOT NULL,  --- if attr in param is blank, remove attr data or remain unchanged
	ValidFrom datetime2 NOT NULL,
	ValidUntil datetime2 NULL
)
 

create table TDX_AdapterAPIUserMapping(
	AccountId bigint NOT NULL,
	ApiUserId bigint NOT NULL,
	TractionCompanyId bigint NOT NULL, 
	TractionEndpointId bigint NOT NULL,
	TractionEndpointUserid varchar(200) NOT NULL,
	TractionEndpointUsername varchar(50) NOT NULL,
	TractionEndpointPassword varchar(50) NOT NULL,
	AllowPublicAccess bit NOT NULL,
	IntergrationServerPassword varchar(20) NOT NULL
)
 

create table TDX_AdapterContactFieldMapping(
	AccountId bigint NOT NULL,
	ContactFieldId bigint NOT NULL,
	TractionCompanyId bigint NOT NULL, 
	TractionCustomerAttrId varchar(50) NULL, 
	TractionCustomerAttrCode varchar(100) NULL, 
	TractionCustomerAttrLabel varchar(100) NOT NULL
)
 

create table TDX_AdapterContactListMapping(
	AccountId bigint NOT NULL,
	ContactListId bigint NOT NULL,
	TractionCompanyId bigint NOT NULL,
	TractionFunctionId bigint NULL,
	TractionSubscriptionId bigint NOT NULL,
	SubscriptionGroupInParam varchar(100) NULL, --- 'subscriptions','subscription','emailSubscription', 'subscriptionId', etc..
	SubscriptionInParam varchar(100) NULL,
	SubscriptionValueTypeInParam varchar(20) NULL, --- 'Scalar', 'Array', 'Map'
	AllowNumericId bit NOT NULL,
	IgnoreInvalid bit NOT NULL,
	SubWord varchar(20) NULL, 
	UnsubWord varchar(20) NULL
)
 

create table TDX_DomainCert (
	Id bigint PRIMARY KEY IDENTITY NOT NULL,
	AccountId bigint NOT NULL,
	Domain nvarchar(100) NOT NULL,
	DomainKey nvarchar(255) NOT NULL,
	DomainKeyContent nvarchar(255) NOT NULL,
)



";

#endregion

#region Script to check if there are any columns missing from the database

private const string CHECK_MISSING_COLUMNS = @"
declare @problem_columns varchar(max)
set @problem_columns = ''

-- The idea here is to get a full list of all column names in both databases.
-- join the two lists with a full outer join, in order to find which columns
-- appear in only one table. Most of the code here is actually just text formatting.

select
	-- Format the error message by adding commas and newlines
	@problem_columns = @problem_columns + ', ' + char(13) + char(10) +
	-- Get a description for the column that is present in only one database.
	-- Where one of the columns in this result set is null, the column appears in only one database.
	case
		when cleanDB.columnname is null then 'Zebra_AlterScript_ScriptRun.dbo.' + scriptDB.columnname
		when scriptDB.columnname is null then 'Zebra_AlterScript_CleanTable.dbo.' + cleanDB.columnname
		else NULL
	end

from
(
	-- Select all table and column names from the first database
	select tabs.name + '.' + cols.name as columnname
	from Zebra_AlterScript_CleanTable.sys.columns as cols
	join Zebra_AlterScript_CleanTable.sys.tables
		as tabs on cols.object_id = tabs.object_id
) as cleanDB
full outer join
(
	-- Select all table and column names from the second database
	select tabs.name + '.' + cols.name as columnname
	from Zebra_AlterScript_ScriptRun.sys.columns as cols
	join Zebra_AlterScript_ScriptRun.sys.tables
		as tabs on cols.object_id = tabs.object_id
	where tabs.name not like '%_deprecated'
) as scriptDB
on scriptDB.columnname = cleanDB.columnname
-- Select only columns that appear in only one of the two databases
where cleanDB.columnname is null or scriptDB.columnname is null


if len(@problem_columns) > 5
begin
-- Cut off the leading comma, space and newline if necessary
	set @problem_columns = substring(@problem_columns, 5, len(@problem_columns))

	set @problem_columns = 'The following columns appear in one database but not the other: ' + char(13) + char(10) + @problem_columns

	-- An error of level 11 is the lowest level of error that sqlcmd.exe will output as an error
	raiserror(@problem_columns, 11, 1)
end
";

#endregion

#region Script to check if there are any differences between columns in the database

private const string CHECK_TABLE_DIFFERENCES = @"
-- This script compares the columns in two databases, to make sure they are identical.
-- it will NOT detect missing columns from one of the databases, only columns that
-- exist in both databases will be compared.

-- Columns in sys.columns that store object id's cannot be compared, because these should always
-- be different between two databases.

declare @problem_columns varchar(max)
set @problem_columns = ''

select 
	-- Format the error message by adding commas and newlines
	@problem_columns = @problem_columns + ', ' + char(13) + char(10) + columnDiff
from
(
	select
		-- Compare all the columns in both tables, in order to figure out how the columns differ.
		-- This will only find ONE column that is different, so will only report one problem at a time.
		case 
			when clean_name						<>	script_name						then clean_fullColumnName + ' name differs'
			when clean_system_type_id			<>	script_system_type_id			then clean_fullColumnName + ' system_type_id differs'
			when clean_user_type_id				<>	script_user_type_id				then clean_fullColumnName + ' user_type_id differs'
			when clean_max_length				<>	script_max_length				then clean_fullColumnName + ' max_length differs'
			when clean_precision				<>	script_precision				then clean_fullColumnName + ' precision differs'
			when clean_scale					<>	script_scale					then clean_fullColumnName + ' scale differs'
			when clean_collation_name			<>	script_collation_name			then clean_fullColumnName + ' collation_name differs'
			when clean_is_nullable				<>	script_is_nullable				then clean_fullColumnName + ' is_nullable differs'
			when clean_is_ansi_padded			<>	script_is_ansi_padded			then clean_fullColumnName + ' is_ansi_padded differs'
			when clean_is_rowguidcol			<>	script_is_rowguidcol			then clean_fullColumnName + ' is_rowguidcol differs'
			when clean_is_identity				<>	script_is_identity				then clean_fullColumnName + ' is_identity differs'
			when clean_is_computed				<>	script_is_computed				then clean_fullColumnName + ' is_computed differs'
			when clean_is_filestream			<>	script_is_filestream			then clean_fullColumnName + ' is_filestream differs'
			when clean_is_replicated			<>	script_is_replicated			then clean_fullColumnName + ' is_replicated differs'
			when clean_is_non_sql_subscribed	<>	script_is_non_sql_subscribed	then clean_fullColumnName + ' is_non_sql_subscribed differs'
			when clean_is_merge_published		<>	script_is_merge_published		then clean_fullColumnName + ' is_merge_published differs'
			when clean_is_dts_replicated		<>	script_is_dts_replicated		then clean_fullColumnName + ' is_dts_replicated differs'
			when clean_is_xml_document			<>	script_is_xml_document			then clean_fullColumnName + ' is_xml_document differs'
			when clean_xml_collection_id		<>	script_xml_collection_id		then clean_fullColumnName + ' xml_collection_id differs'
			else NULL
		end as columnDiff
	from
	(
		select tabs.name + '.' + cols.name as clean_fullColumnName,
		--	cols.object_id,
			cols.name						   as clean_name,
		--	cols.column_id,
			cols.system_type_id				   as clean_system_type_id,
			cols.user_type_id				   as clean_user_type_id,
			cols.max_length					   as clean_max_length,
			cols.precision					   as clean_precision,
			cols.scale						   as clean_scale,
			cols.collation_name				   as clean_collation_name,
			cols.is_nullable				   as clean_is_nullable,
			cols.is_ansi_padded				   as clean_is_ansi_padded,
			cols.is_rowguidcol				   as clean_is_rowguidcol,
			cols.is_identity				   as clean_is_identity,
			cols.is_computed				   as clean_is_computed,
			cols.is_filestream				   as clean_is_filestream,
			cols.is_replicated				   as clean_is_replicated,
			cols.is_non_sql_subscribed		   as clean_is_non_sql_subscribed,
			cols.is_merge_published			   as clean_is_merge_published,
			cols.is_dts_replicated			   as clean_is_dts_replicated,
			cols.is_xml_document			   as clean_is_xml_document,
			cols.xml_collection_id			   as clean_xml_collection_id
		--	cols.default_object_id,
		--	cols.rule_object_id
		from Zebra_AlterScript_CleanTable.sys.columns as cols
		join Zebra_AlterScript_CleanTable.sys.tables
			as tabs on cols.object_id = tabs.object_id
	) as cleanDB
	join
	(
		select tabs.name + '.' + cols.name as script_fullColumnName,
		--	cols.object_id,
			cols.name						   as script_name,
		--	cols.column_id,
			cols.system_type_id				   as script_system_type_id,
			cols.user_type_id				   as script_user_type_id,
			cols.max_length					   as script_max_length,
			cols.precision					   as script_precision,
			cols.scale						   as script_scale,
			cols.collation_name				   as script_collation_name,
			cols.is_nullable				   as script_is_nullable,
			cols.is_ansi_padded				   as script_is_ansi_padded,
			cols.is_rowguidcol				   as script_is_rowguidcol,
			cols.is_identity				   as script_is_identity,
			cols.is_computed				   as script_is_computed,
			cols.is_filestream				   as script_is_filestream,
			cols.is_replicated				   as script_is_replicated,
			cols.is_non_sql_subscribed		   as script_is_non_sql_subscribed,
			cols.is_merge_published			   as script_is_merge_published,
			cols.is_dts_replicated			   as script_is_dts_replicated,
			cols.is_xml_document			   as script_is_xml_document,
			cols.xml_collection_id			   as script_xml_collection_id
		--	cols.default_object_id,
		--	cols.rule_object_id
		from Zebra_AlterScript_ScriptRun.sys.columns as cols
		join Zebra_AlterScript_ScriptRun.sys.tables
			as tabs on cols.object_id = tabs.object_id
		where tabs.name not like '%_deprecated'
	) as scriptDB
	on cleanDB.clean_fullColumnName = scriptDB.script_fullColumnName
) as columnDifferences
where columnDiff is not null


-- There was at least one problem, so raise an error:
if len(@problem_columns) > 5
begin
	-- Cut off the leading comma, space and newline if necessary
	set @problem_columns = substring(@problem_columns, 5, len(@problem_columns))

	set @problem_columns = 'The following differences exist between columns: ' + char(13) + char(10) + @problem_columns

	raiserror(@problem_columns, 11, 1)
end
";

#endregion

	}
}
