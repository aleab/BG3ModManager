﻿using DynamicData;
using DynamicData.Binding;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Newtonsoft.Json;
using System.Runtime.Serialization;
using System.Windows.Input;
using DivinityModManager.Util;

namespace DivinityModManager.Models
{
	[DataContract]
	public class DivinityModManagerSettings : ReactiveObject
	{
		private string gameDataPath = "";

		[DataMember]
		public string GameDataPath
		{
			get => gameDataPath;
			set 
			{
				if (value != gameDataPath) CanSaveSettings = true;
				this.RaiseAndSetIfChanged(ref gameDataPath, value);
			}
		}

		private string dos2workshopPath = "";

		[DataMember]
		public string DOS2WorkshopPath
		{
			get => dos2workshopPath;
			set 
			{
				if (value != dos2workshopPath) CanSaveSettings = true;
				this.RaiseAndSetIfChanged(ref dos2workshopPath, value);
			}
		}

		private string loadOrderPath = "";

		[DataMember]
		public string LoadOrderPath
		{
			get => loadOrderPath;
			set 
			{
				if (value != loadOrderPath) CanSaveSettings = true;
				this.RaiseAndSetIfChanged(ref loadOrderPath, value); 
			}
		}

		private bool logEnabled = false;

		public bool LogEnabled
		{
			get => logEnabled;
			set { this.RaiseAndSetIfChanged(ref logEnabled, value); }
		}

		public ICommand SaveSettingsCommand { get; set; }
		public ICommand OpenSettingsFolderCommand { get; set; }

		private bool canSaveSettings = false;

		public bool CanSaveSettings
		{
			get => canSaveSettings;
			set { this.RaiseAndSetIfChanged(ref canSaveSettings, value); }
		}

		public DivinityModManagerSettings()
		{
			OpenSettingsFolderCommand = ReactiveCommand.Create(() =>
			{
				Process.Start(DivinityApp.SettingsFile);
			});
		}
	}
}
