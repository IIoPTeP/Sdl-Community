﻿using Microsoft.Win32.TaskScheduler;
using Sdl.Community.BackupService.Helpers;
using Sdl.Community.BackupService.Models;
using System;
using System.IO;
using static Sdl.Community.BackupService.Helpers.Enums;
using System.Globalization;

namespace Sdl.Community.BackupService
{
	public class Service
	{
		public JsonRequestModel GetJsonInformation()
		{
			Persistence persistence = new Persistence();
			JsonRequestModel result = persistence.ReadFormInformation();

			return result;
		}

		// Create task scheduler for the backup files process.
		public void CreateTaskScheduler()
		{
			var jsonRequestModel = GetJsonInformation();

			DateTime startDate = DateTime.Now;
			var tr = Trigger.CreateTrigger(TaskTriggerType.Time);

			if (jsonRequestModel != null && jsonRequestModel.ChangeSettingsModel != null)
			{
				// Create a new task definition for the local machine and assign properties
				TaskDefinition td = TaskService.Instance.NewTask();
				td.RegistrationInfo.Description = "Backup files";

				if (jsonRequestModel.ChangeSettingsModel.IsPeriodicOptionChecked && jsonRequestModel.PeriodicBackupModel != null)
				{
					AddPeriodicTimeScheduler(jsonRequestModel, startDate, td, tr);
				}
				if (jsonRequestModel.ChangeSettingsModel.IsManuallyOptionChecked && jsonRequestModel.PeriodicBackupModel != null)
				{
					AddManuallyTimeScheduler(td, tr);
				}
			}
		}

		// Add trigger which executes the backup files console application.
		private void AddTrigger(Trigger trigger, TaskDefinition td)
		{
			using (TaskService ts = new TaskService())
			{
				td.Triggers.Add(trigger);

				td.Actions.Add(new ExecAction(Path.Combine(Constants.DeployPath, "Sdl.Community.BackupFiles.exe"), "Daily"));

				try
				{
					ts.RootFolder.RegisterTaskDefinition("DailyScheduler", td);
				}
				catch (Exception ex)
				{
					MessageLogger.LogFileMessage(ex.Message);
				}
			}
		}

		// Add periodic time scheduler depending on user setup.
		private void AddPeriodicTimeScheduler(JsonRequestModel jsonRequestModel, DateTime startDate, TaskDefinition td, Trigger tr)
		{
			DateTime atScheduleTime = DateTime.Parse(jsonRequestModel.PeriodicBackupModel.BackupAt, CultureInfo.InvariantCulture);
			tr.StartBoundary = jsonRequestModel.PeriodicBackupModel.FirstBackup.Date + new TimeSpan(atScheduleTime.Hour, atScheduleTime.Minute, atScheduleTime.Second);

			SetupRealDateTime(tr);

			if (jsonRequestModel.PeriodicBackupModel.TimeType.Equals(Enums.GetDescription(TimeTypes.Hours)))
			{
				tr.Repetition.Interval = TimeSpan.FromHours(jsonRequestModel.PeriodicBackupModel.BackupInterval);
				AddTrigger(tr, td);
			}

			if (jsonRequestModel.PeriodicBackupModel.TimeType.Equals(Enums.GetDescription(TimeTypes.Minutes)))
			{
				tr.Repetition.Interval = TimeSpan.FromMinutes(jsonRequestModel.PeriodicBackupModel.BackupInterval);
				AddTrigger(tr, td);
			}
		}

		private void AddManuallyTimeScheduler(TaskDefinition td, Trigger tr)
		{
			tr.StartBoundary = DateTime.Now.Date + new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);

			SetupRealDateTime(tr);

			tr.Repetition.Interval = TimeSpan.FromMinutes(2);
			tr.EndBoundary = DateTime.Now.AddMinutes(10); ;
			AddTrigger(tr, td);
		}

		// Method used in order to start trigger at the current date time when Now button is pressed in the Periodic window.
		// The 10 seconds are added as a short delay to ensure that the backup is done at the current date time after the Main window is closed.
		private void SetupRealDateTime(Trigger tr)
		{
			var dateTimeResult = DateTime.Compare(tr.StartBoundary, DateTime.UtcNow);

			if (dateTimeResult > 0)
			{
				tr.StartBoundary = DateTime.UtcNow.AddSeconds(10);
			}
		}
	}
}