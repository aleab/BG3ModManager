using Alphaleonis.Win32.Filesystem;

using DivinityModManager.Models.Github;
using DivinityModManager.Models.NexusMods;
using DivinityModManager.Util;

using DynamicData;
using DynamicData.Aggregation;

using ReactiveUI;
using ReactiveUI.Fody.Helpers;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Windows;

namespace DivinityModManager.Models
{
	[DataContract]
	[ScreenReaderHelper(Name = "DisplayName", HelpText = "HelpText")]
	public class DivinityModData : DivinityBaseModData, ISelectable
	{
		[Reactive][DataMember] public int Index { get; set; }

		[DataMember(Name = "FileName")]
		public string OutputPakName
		{
			get
			{
				if (!Folder.Contains(UUID))
				{
					return Path.ChangeExtension($"{Folder}_{UUID}", "pak");
				}
				else
				{
					return Path.ChangeExtension($"{FileName}", "pak");
				}
			}
		}

		[Reactive][DataMember(Name = "Type")] public string ModType { get; set; }

		[DataMember] public List<string> Modes { get; set; } = new List<string>();

		[DataMember] public string Targets { get; set; }
		[Reactive] public DateTime? LastUpdated { get; set; }

		[Reactive] public DivinityExtenderModStatus ExtenderModStatus { get; set; }
		[Reactive] public DivinityOsirisModStatus OsirisModStatus { get; set; }

		[Reactive] public int CurrentExtenderVersion { get; set; }

		private string ExtenderStatusToToolTipText(DivinityExtenderModStatus status, int requiredVersion, int currentVersion)
		{
			var result = "";
			switch (status)
			{
				case DivinityExtenderModStatus.REQUIRED:
				case DivinityExtenderModStatus.REQUIRED_MISSING:
				case DivinityExtenderModStatus.REQUIRED_DISABLED:
				case DivinityExtenderModStatus.REQUIRED_OLD:
					if (status == DivinityExtenderModStatus.REQUIRED_MISSING)
					{
						result = "[MISSING] ";
					}
					else if (status == DivinityExtenderModStatus.REQUIRED_DISABLED)
					{
						result = "[EXTENSIONS DISABLED] ";
					}
					else if (status == DivinityExtenderModStatus.REQUIRED_OLD)
					{
						result = "[OLD] ";
					}
					if (requiredVersion > -1)
					{
						result += $"Requires Script Extender v{requiredVersion} or Higher";
					}
					else
					{
						result += "Requires the Script Extender";
					}
					if (status == DivinityExtenderModStatus.REQUIRED_DISABLED)
					{
						result += " (Enable Extensions in the Script Extender config)";
					}
					if (status == DivinityExtenderModStatus.REQUIRED_OLD)
					{
						result += " (Update by running the game)";
					}
					break;
				case DivinityExtenderModStatus.SUPPORTS:
					if (requiredVersion > -1)
					{
						result = $"Uses Script Extender v{requiredVersion} or Higher (Optional)";
					}
					else
					{
						result = "Uses the Script Extender (Optional)";
					}
					break;
				case DivinityExtenderModStatus.NONE:
				default:
					result = "";
					break;
			}
			if (result != "")
			{
				result += Environment.NewLine;
			}
			if (currentVersion > -1)
			{
				result += $"(Currently installed version is v{currentVersion})";
			}
			else
			{
				result += "(No installed extender version found)";
			}
			return result;
		}

		[DataMember] [Reactive] public DivinityModScriptExtenderConfig ScriptExtenderData { get; set; }
		[DataMember] public SourceList<DivinityModDependencyData> Dependencies { get; set; } = new SourceList<DivinityModDependencyData>();

		protected ReadOnlyObservableCollection<DivinityModDependencyData> displayedDependencies;
		public ReadOnlyObservableCollection<DivinityModDependencyData> DisplayedDependencies => displayedDependencies;

		public override string GetDisplayName()
		{
			if (DisplayFileForName)
			{
				if (!IsEditorMod)
				{
					return FileName;
				}
				else
				{
					return Folder + " [Editor Project]";
				}
			}
			else
			{
				if (!DivinityApp.DeveloperModeEnabled && UUID == DivinityApp.MAIN_CAMPAIGN_UUID)
				{
					return "Main";
				}
				return Name;
			}
		}

		private readonly ObservableAsPropertyHelper<bool> hasToolTip;

		public bool HasToolTip => hasToolTip.Value;

		private readonly ObservableAsPropertyHelper<int> dependencyCount;
		public int TotalDependencies => dependencyCount.Value;

		private readonly ObservableAsPropertyHelper<bool> hasDependencies;

		public bool HasDependencies => hasDependencies.Value;

		[Reactive] public bool HasScriptExtenderSettings { get; set; }

		[Reactive] public bool IsEditorMod { get; set; }

		[Reactive] public bool IsActive { get; set; }

		private bool isSelected = false;

		public bool IsSelected
		{
			get => isSelected;
			set
			{
				if (value && Visibility != Visibility.Visible)
				{
					value = false;
				}
				this.RaiseAndSetIfChanged(ref isSelected, value);
			}
		}

		private readonly ObservableAsPropertyHelper<bool> _canDelete;
		public bool CanDelete => _canDelete.Value;

		private readonly ObservableAsPropertyHelper<bool> _canAddToLoadOrder;
		public bool CanAddToLoadOrder => _canAddToLoadOrder.Value;

		private readonly ObservableAsPropertyHelper<bool> _canOpenWorkshopLink;
		public bool CanOpenWorkshopLink => _canOpenWorkshopLink.Value;

		private readonly ObservableAsPropertyHelper<string> _extenderSupportToolTipText;
		public string ScriptExtenderSupportToolTipText => _extenderSupportToolTipText.Value;

		private readonly ObservableAsPropertyHelper<string> _osirisStatusToolTipText;
		public string OsirisStatusToolTipText => _osirisStatusToolTipText.Value;

		private readonly ObservableAsPropertyHelper<string> _xmlPatcherStatusToolTipText;
		public string XmlPatcherStatusToolTipText => _xmlPatcherStatusToolTipText.Value;

		private readonly ObservableAsPropertyHelper<string> _lastModifiedDateText;
		public string LastModifiedDateText => _lastModifiedDateText.Value;

		private readonly ObservableAsPropertyHelper<Visibility> dependencyVisibility;
		public Visibility DependencyVisibility => dependencyVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _openWorkshopLinkVisibility;
		public Visibility OpenWorkshopLinkVisibility => _openWorkshopLinkVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _openNexusModsLinkVisibility;
		public Visibility OpenNexusModsLinkVisibility => _openNexusModsLinkVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _toggleForceAllowInLoadOrderVisibility;
		public Visibility ToggleForceAllowInLoadOrderVisibility => _toggleForceAllowInLoadOrderVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _extenderStatusVisibility;
		public Visibility ExtenderStatusVisibility => _extenderStatusVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _osirisStatusVisibility;
		public Visibility OsirisStatusVisibility => _osirisStatusVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _xmlPatcherStatusVisibility;
		public Visibility XmlPatcherStatusVisibility => _xmlPatcherStatusVisibility.Value;

		#region NexusMods Properties

		private readonly ObservableAsPropertyHelper<bool> _canOpenNexusModsLink;
		public bool CanOpenNexusModsLink => _canOpenNexusModsLink.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _nexusImageVisibility;
		public Visibility NexusImageVisibility => _nexusImageVisibility.Value;

		private readonly ObservableAsPropertyHelper<Visibility> _nexusModsInformationVisibility;
		public Visibility NexusModsInformationVisibility => _nexusModsInformationVisibility.Value;

		private readonly ObservableAsPropertyHelper<DateTime> _nexusModsCreatedDate;
		public DateTime NexusModsCreatedDate => _nexusModsCreatedDate.Value;

		private readonly ObservableAsPropertyHelper<DateTime> _nexusModsUpdatedDate;
		public DateTime NexusModsUpdatedDate => _nexusModsUpdatedDate.Value;

		private readonly ObservableAsPropertyHelper<string> _nexusModsTooltipInfo;
		public string NexusModsTooltipInfo => _nexusModsTooltipInfo.Value;

		#endregion

		[Reactive] public bool WorkshopEnabled { get; set; }
		[Reactive] public bool NexusModsEnabled { get; set; }
		[Reactive] public bool CanDrag { get; set; }
		[Reactive] public bool DeveloperMode { get; set; }
		[Reactive] public bool HasColorOverride { get; set; }
		[Reactive] public string SelectedColor { get; set; }
		[Reactive] public string ListColor { get; set; }

		public HashSet<string> Files { get; set; }
		[Reactive] public IReadOnlyCollection<string> XmlPatcherFiles { get; set; }

		[Reactive] public DivinityModWorkshopData WorkshopData { get; set; }
		[Reactive] public NexusModsModData NexusModsData { get; set; }
		[Reactive] public GithubModData GithubData { get; set; }

		public string GetURL(ModSourceType modSourceType, bool asProtocol = false)
		{
			switch (modSourceType)
			{
				case ModSourceType.STEAM:
					if (WorkshopData != null && WorkshopData.ID != "")
					{
						if (!asProtocol)
						{
							return $"https://steamcommunity.com/sharedfiles/filedetails/?id={WorkshopData.ID}";
						}
						else
						{
							return $"steam://url/CommunityFilePage/{WorkshopData.ID}";
						}
					}
					break;
				case ModSourceType.NEXUSMODS:
					if (NexusModsData != null && NexusModsData.ModId >= DivinityApp.NEXUSMODS_MOD_ID_START)
					{
						return String.Format(DivinityApp.NEXUSMODS_MOD_URL, NexusModsData.ModId);
					}
					break;
				case ModSourceType.GITHUB:
					if (GithubData != null)
					{
						return $"https://github.com/{GithubData.Author}/{GithubData.Repository}";
					}
					break;
			}
			return "";
		}

		public List<string> GetAllURLs(bool asProtocol = false)
		{
			var urls = new List<string>();
			var steamUrl = GetURL(ModSourceType.STEAM, asProtocol);
			if (!String.IsNullOrEmpty(steamUrl))
			{
				urls.Add(steamUrl);
			}
			var nexusUrl = GetURL(ModSourceType.NEXUSMODS, asProtocol);
			if (!String.IsNullOrEmpty(nexusUrl))
			{
				urls.Add(nexusUrl);
			}
			var githubUrl = GetURL(ModSourceType.GITHUB, asProtocol);
			if (!String.IsNullOrEmpty(githubUrl))
			{
				urls.Add(githubUrl);
			}
			return urls;
		}

		public override string ToString()
		{
			return $"Name({Name}) Version({Version?.Version}) Author({Author}) UUID({UUID})";
		}

		public DivinityLoadOrderEntry ToOrderEntry()
		{
			return new DivinityLoadOrderEntry
			{
				UUID = this.UUID,
				Name = this.Name
			};
		}

		public DivinityProfileActiveModData ToProfileModData()
		{
			return new DivinityProfileActiveModData()
			{
				Folder = Folder,
				MD5 = MD5,
				Name = Name,
				UUID = UUID,
				Version = Version.VersionInt
			};
		}

		public void AllowInLoadOrder(bool b)
		{
			ForceAllowInLoadOrder = b;
			IsActive = b && IsForceLoaded;
		}

		private string OsirisStatusToTooltipText(DivinityOsirisModStatus status)
		{
			switch(status)
			{
				case DivinityOsirisModStatus.SCRIPTS:
					return "Has Osiris Scripting";
				case DivinityOsirisModStatus.MODFIXER:
					return "Has Mod Fixer";
				case DivinityOsirisModStatus.NONE:
				default:
					return "";
			}
		}

		private bool CanOpenWorkshopBoolCheck(bool enabled, bool isHidden, bool isLarianMod, string workshopID)
		{
			return enabled && !isHidden & !isLarianMod & !String.IsNullOrEmpty(workshopID);
		}

		private string NexusModsInfoToTooltip(DateTime createdDate, DateTime updatedDate, long endorsements)
		{
			var lines = new List<String>();

			if (endorsements > 0)
			{
				lines.Add($"Endorsements: {endorsements}");
			}

			if (createdDate != DateTime.MinValue)
			{
				lines.Add($"Created on {createdDate.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}");
			}

			if(updatedDate != DateTime.MinValue)
			{
				lines.Add($"Last updated on {createdDate.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}");
			}

			return String.Join("\n", lines);
		}

		private string XmlPatcherStatusTooltipText(IReadOnlyCollection<string> files)
		{
			return (files?.Count ?? 0) > 0 ? "Contains UI patches" : "";
		}

		public DivinityModData(bool isBaseGameMod = false) : base()
		{
			Targets = "";
			Index = -1;
			CanDrag = true;

			WorkshopData = new DivinityModWorkshopData();
			NexusModsData = new NexusModsModData();
			//GithubData = new GithubModData();

			this.WhenAnyValue(x => x.UUID).BindTo(NexusModsData, x => x.UUID);

			_nexusImageVisibility = this.WhenAnyValue(x => x.NexusModsData.PictureUrl)
				.Select(uri => uri != null && !String.IsNullOrEmpty(uri.AbsolutePath) ? Visibility.Visible : Visibility.Collapsed)
				.StartWith(Visibility.Collapsed)
				.ToProperty(this, nameof(NexusImageVisibility), scheduler: RxApp.MainThreadScheduler);

			_nexusModsInformationVisibility = this.WhenAnyValue(x => x.NexusModsData.IsUpdated)
				.Select(b => b ? Visibility.Visible : Visibility.Collapsed)
				.StartWith(Visibility.Collapsed)
				.ToProperty(this, nameof(NexusModsInformationVisibility), scheduler: RxApp.MainThreadScheduler);

			_nexusModsCreatedDate = this.WhenAnyValue(x => x.NexusModsData.CreatedTimestamp).SkipWhile(x => x <= 0).Select(x => DateUtils.UnixTimeStampToDateTime(x)).ToProperty(this, nameof(NexusModsCreatedDate));
			_nexusModsUpdatedDate = this.WhenAnyValue(x => x.NexusModsData.UpdatedTimestamp).SkipWhile(x => x <= 0).Select(x => DateUtils.UnixTimeStampToDateTime(x)).ToProperty(this, nameof(NexusModsUpdatedDate));

			_nexusModsTooltipInfo = this.WhenAnyValue(x => x.NexusModsCreatedDate, x => x.NexusModsUpdatedDate, x => x.NexusModsData.EndorsementCount)
				.Select(x => NexusModsInfoToTooltip(x.Item1, x.Item2, x.Item3)).ToProperty(this, nameof(NexusModsTooltipInfo));

			_toggleForceAllowInLoadOrderVisibility = this.WhenAnyValue(x => x.IsForceLoaded, x => x.HasMetadata, x => x.IsForceLoadedMergedMod)
				.Select(b => b.Item1 && b.Item2 && !b.Item3 ? Visibility.Visible : Visibility.Collapsed)
				.StartWith(Visibility.Collapsed)
				.ToProperty(this, nameof(ToggleForceAllowInLoadOrderVisibility), scheduler: RxApp.MainThreadScheduler);

			_canOpenWorkshopLink = this.WhenAnyValue(x => x.WorkshopEnabled, x => x.IsHidden, x => x.IsLarianMod, x => x.WorkshopData.ID, CanOpenWorkshopBoolCheck).ToProperty(this, nameof(CanOpenWorkshopLink));
			_openWorkshopLinkVisibility = this.WhenAnyValue(x => x.CanOpenWorkshopLink)
				.Select(b => b ? Visibility.Visible : Visibility.Collapsed)
				.StartWith(Visibility.Collapsed)
				.ToProperty(this, nameof(OpenWorkshopLinkVisibility), scheduler: RxApp.MainThreadScheduler);

			_canOpenNexusModsLink = this.WhenAnyValue(x => x.NexusModsEnabled, x => x.NexusModsData.ModId, (b, id) => b && id >= DivinityApp.NEXUSMODS_MOD_ID_START).ToProperty(this, nameof(CanOpenNexusModsLink));
			_openNexusModsLinkVisibility = this.WhenAnyValue(x => x.CanOpenNexusModsLink)
				.Select(b => b ? Visibility.Visible : Visibility.Collapsed)
				.StartWith(Visibility.Collapsed)
				.ToProperty(this, nameof(OpenNexusModsLinkVisibility), scheduler: RxApp.MainThreadScheduler);

			var connection = this.Dependencies.Connect().ObserveOn(RxApp.MainThreadScheduler);
			connection.Bind(out displayedDependencies).DisposeMany().Subscribe();
			dependencyCount = connection.Count().StartWith(0).ToProperty(this, nameof(TotalDependencies));
			hasDependencies = this.WhenAnyValue(x => x.TotalDependencies, c => c > 0).StartWith(false).ToProperty(this, nameof(HasDependencies));
			dependencyVisibility = this.WhenAnyValue(x => x.HasDependencies, b => b ? Visibility.Visible : Visibility.Collapsed).StartWith(Visibility.Collapsed).ToProperty(this, nameof(DependencyVisibility));
			this.WhenAnyValue(x => x.IsActive, x => x.IsForceLoaded, x => x.IsForceLoadedMergedMod,
				x => x.ForceAllowInLoadOrder).Subscribe((b) =>
			{
				var isActive = b.Item1;
				var isForceLoaded = b.Item2;
				var isForceLoadedMergedMod = b.Item3;
				var forceAllowInLoadOrder = b.Item4;

				if(forceAllowInLoadOrder || isActive)
				{
					CanDrag = true;
				}
				else
				{
					CanDrag = !isForceLoaded || isForceLoadedMergedMod;
				}
			});

			this.WhenAnyValue(x => x.IsForceLoaded, x => x.IsEditorMod).Subscribe((b) =>
			{
				var isForceLoaded = b.Item1;
				var isEditorMod = b.Item2;

				if (isForceLoaded)
				{
					this.SelectedColor = "#64F38F00";
					this.ListColor = "#32C17200";
					HasColorOverride = true;
				}
				else if (isEditorMod)
				{
					this.SelectedColor = "#6400ED48";
					this.ListColor = "#0C00FF4D";
					HasColorOverride = true;
				}
				else
				{
					HasColorOverride = false;
				}
			});

			if (isBaseGameMod)
			{
				this.IsHidden = true;
				this.IsLarianMod = true;
			}

			// If a screen reader is active, don't bother making tooltips for the mod item entry
			hasToolTip = this.WhenAnyValue(x => x.Description, x => x.HasDependencies, x => x.UUID).
				Select(x => !DivinityApp.IsScreenReaderActive() && (
				!String.IsNullOrEmpty(x.Item1) || x.Item2 || !String.IsNullOrEmpty(x.Item3))).StartWith(true).ToProperty(this, nameof(HasToolTip));

			_canDelete = this.WhenAnyValue(x => x.IsEditorMod, x => x.IsLarianMod, x => x.FilePath,
				(isEditorMod, isLarianMod, path) => !isEditorMod && !isLarianMod && File.Exists(path)).StartWith(false).ToProperty(this, nameof(CanDelete));
			_canAddToLoadOrder = this.WhenAnyValue(x => x.ModType, x => x.IsLarianMod, x => x.IsForceLoaded, x => x.IsForceLoadedMergedMod, x => x.ForceAllowInLoadOrder,
				(modType, isLarianMod, isForceLoaded, isMergedMod, forceAllowInLoadOrder) => modType != "Adventure" && !isLarianMod && (!isForceLoaded || isMergedMod) || forceAllowInLoadOrder).StartWith(true).ToProperty(this, nameof(CanAddToLoadOrder));

			var whenExtenderProp = this.WhenAnyValue(x => x.ExtenderModStatus, x => x.ScriptExtenderData.RequiredVersion, x => x.CurrentExtenderVersion);
			_extenderSupportToolTipText = whenExtenderProp.Select(x => ExtenderStatusToToolTipText(x.Item1, x.Item2, x.Item3)).ToProperty(this, nameof(ScriptExtenderSupportToolTipText), true, RxApp.MainThreadScheduler);
			_extenderStatusVisibility = this.WhenAnyValue(x => x.ExtenderModStatus).Select(x => x != DivinityExtenderModStatus.NONE ? Visibility.Visible : Visibility.Collapsed).ToProperty(this, nameof(ExtenderStatusVisibility), true, RxApp.MainThreadScheduler);

			var whenOsirisStatusChanges = this.WhenAnyValue(x => x.OsirisModStatus);
			_osirisStatusVisibility = whenOsirisStatusChanges.Select(x => x != DivinityOsirisModStatus.NONE ? Visibility.Visible : Visibility.Collapsed).ToProperty(this, nameof(OsirisStatusVisibility), true, RxApp.MainThreadScheduler);
			_osirisStatusToolTipText = whenOsirisStatusChanges.Select(OsirisStatusToTooltipText).ToProperty(this, nameof(OsirisStatusToolTipText), true, RxApp.MainThreadScheduler);
			ExtenderModStatus = DivinityExtenderModStatus.NONE;
			OsirisModStatus = DivinityOsirisModStatus.NONE;

			_lastModifiedDateText = this.WhenAnyValue(x => x.LastUpdated).SkipWhile(x => !x.HasValue)
				.Select(x => $"Last Modified on {x.Value.ToString(DivinityApp.DateTimeColumnFormat, CultureInfo.InstalledUICulture)}")
				.StartWith("")
				.ToProperty(this, nameof(LastModifiedDateText), scheduler:RxApp.MainThreadScheduler);

			_xmlPatcherStatusVisibility = this.WhenAnyValue(x => x.XmlPatcherFiles)
			   .Select(x => (x?.Count ?? 0) > 0 ? Visibility.Visible : Visibility.Collapsed).StartWith(Visibility.Collapsed).ToProperty(this, nameof(XmlPatcherStatusVisibility), scheduler: RxApp.MainThreadScheduler);
			_xmlPatcherStatusToolTipText = this.WhenAnyValue(x => x.XmlPatcherFiles).Select(XmlPatcherStatusTooltipText).ToProperty(this, nameof(XmlPatcherStatusToolTipText), scheduler: RxApp.MainThreadScheduler);
		}
	}
}
