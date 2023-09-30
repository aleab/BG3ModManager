using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DivinityModManager.Models;
using DivinityModManager.Models.XamlPatcher;
using DivinityModManager.Properties;
using DynamicData;
using LSLib.LS;
using LSLib.LS.Enums;
using Tizuby.XmlPatchLib;

namespace DivinityModManager.Util
{
	public sealed class XamlPatcher
	{
		private static readonly PackageCreationOptions PackageCreationOptions = new PackageCreationOptions
		{
			Compression = CompressionMethod.LZ4,
			Version = PackageVersion.V18,
			Priority = 255,
		};

		private readonly string gameDataPath;
		private readonly IOrderedEnumerable<string> gamePakFiles;
		private readonly XmlPatcher patcher;

		public XamlPatcher(string gameDataPath)
		{
			this.gameDataPath = gameDataPath;
			patcher = new XmlPatcher(new XmlPatcherOptions
			{
				AllowMultiNodeSelectors = true,
				DisableReplaceRestrictions = true,
				UseBestEffort = true,
				UseProcessingInstrutions = true,
			});

			gamePakFiles = Directory.GetFiles(gameDataPath, "*.pak", SearchOption.TopDirectoryOnly)
			   .Select(x => Path.GetFileName(x))
			   .Where(x => x == "Game.pak" || x.StartsWith("Patch"))
			   .OrderByDescending(x => x, StringComparer.Ordinal);
		}

		public XamlPatcherResultData Patch(IEnumerable<DivinityModData> mods)
		{
			var tmpDir = Path.Combine(Path.GetTempPath(), "bg3mm-XamlPatcher");
			if (Directory.Exists(tmpDir))
				Directory.Delete(tmpDir, true);
			Directory.CreateDirectory(tmpDir);

			// { xamlFile -> [(mod, packagedPatchFilePath, patchFilePath)] }
			var patches = new Dictionary<string, IList<(DivinityModData, string, string)>>();
			var xmlPatcherErrors = new List<XmlPatcherError>();
			var errors = new List<XamlPatcherError>();

			// Extract patch files
			foreach (var mod in mods)
			{
				ExtractPatchFiles(mod, tmpDir, patches, errors);
			}

			// Create MergeMod
			var mergeModMetaDir = Directory.CreateDirectory(Path.Combine(tmpDir, "MergeMod/Mods/BG3MM-XamlMergeMod"));
			using (var fs = new FileStream(Path.Combine(mergeModMetaDir.FullName, "meta.lsx"), FileMode.Create, FileAccess.Write))
			{
				var bytes = Resources.XamlPatcher_MergeMod;
				fs.Write(bytes, 0, bytes.Length);
			}

			var mergeModGuiDir = Directory.CreateDirectory(Path.Combine(tmpDir, "MergeMod/Public/Game/GUI"));

			foreach (var kvp in patches)
			{
				// Extract xaml file
				var mergeModXamlFilePath = Path.Combine(mergeModGuiDir.FullName, kvp.Key.Substring(16)); // "Public/Game/GUI/".Length
				if (!ExtractXamlFile(kvp.Key, mergeModXamlFilePath))
				{
					foreach (var (mod, packagedPatchFilePath, _) in kvp.Value)
					{
						errors.Add(new XamlPatcherError(mod, packagedPatchFilePath, $"Trying to patch a file that does not exist: \"{kvp.Key}\""));
					}

					continue;
				}

				// Apply patches
				ApplyPatches(mergeModXamlFilePath, kvp.Value, xmlPatcherErrors, errors);
			}

			// Package MergeMod
			var pakFilePath = Path.Combine(Path.GetTempPath(), "BG3MM-XamlMergeMod.pak");
			if (File.Exists(pakFilePath))
				File.Delete(pakFilePath);
			new Packager().CreatePackage(pakFilePath, Path.Combine(tmpDir, "MergeMod"), PackageCreationOptions);

			// Cleanup
			Directory.Delete(tmpDir, true);

			return new XamlPatcherResultData(pakFilePath, xmlPatcherErrors, errors);
		}

		private bool ExtractXamlFile(string xamlFilePath, string destination)
		{
			var destinationFileInfo = new FileInfo(destination);
			if (!string.IsNullOrWhiteSpace(destinationFileInfo.DirectoryName) && !Directory.Exists(destinationFileInfo.DirectoryName))
				Directory.CreateDirectory(destinationFileInfo.DirectoryName);

			foreach (var pakFileName in gamePakFiles)
			{
				using (var pr = new PackageReader(Path.Combine(gameDataPath, pakFileName)))
				{
					var package = pr.Read();
					var packagedXamlFile = package.Files.Find(x => x.Name == xamlFilePath);
					if (packagedXamlFile == null)
						continue;

					using (var fs = new FileStream(destination, FileMode.Create, FileAccess.Write))
					{
						try
						{
							packagedXamlFile.MakeStream().CopyTo(fs);
						}
						finally
						{
							packagedXamlFile.ReleaseStream();
						}
					}

					return true;
				}
			}

			return false;
		}

		private void ApplyPatches(string xamlFilePath, IEnumerable<(DivinityModData, string, string)> patches, IList<XmlPatcherError> xE, ICollection<XamlPatcherError> E)
		{
			// Remove empty namespace declaration from the xaml document
			var xaml = File.ReadAllText(xamlFilePath, Encoding.UTF8);
			var ns = Regex.Match(xaml, @" xmlns=""([^""]+)""").Groups[1].Value;
			if (!string.IsNullOrWhiteSpace(ns))
				xaml = xaml.Replace($" xmlns=\"{ns}\"", "");

			var xamlDocument = XDocument.Parse(xaml, LoadOptions.PreserveWhitespace);

			foreach (var (mod, packagedPatchFilePath, patchFilePath) in patches)
			{
				try
				{
					var patchDocument = LoadPatchDocument(patchFilePath, xamlDocument);
					var errors = patcher.PatchXml(xamlDocument, patchDocument);
					xE.AddRange(errors);
				}
				catch (Exception ex)
				{
					E.Add(new XamlPatcherError(mod, packagedPatchFilePath, ex.ToString()));
				}
			}

			// Re-add the empty namespace declaration
			if (xamlDocument.Root != null && !string.IsNullOrWhiteSpace(ns) && xamlDocument.Root.Attribute("xmlns") == null)
				xamlDocument.Root.SetAttributeValue("xmlns", ns);

			using (var writer = new XmlTextWriter(xamlFilePath, Encoding.UTF8))
			{
				xamlDocument.WriteTo(writer);
			}
		}

		private static void ExtractPatchFiles(DivinityModData mod, string dir, IDictionary<string, IList<(DivinityModData, string, string)>> patches, ICollection<XamlPatcherError> E)
		{
			var tmpModDir = Directory.CreateDirectory(Path.Combine(dir, mod.UUID));

			using (var pr = new PackageReader(mod.FilePath))
			{
				var package = pr.Read();

				foreach (var packagedPatchFilePath in mod.XmlPatcherFiles)
				{
					try
					{
						// patchFilePath = "Mods/{ModName}/XmlPatcher/**/*.xml"
						var relativePatchFilePath = packagedPatchFilePath.Substring(packagedPatchFilePath.LastIndexOf("XmlPatcher/", StringComparison.InvariantCulture) + 11);
						var xamlFilePath = $"Public/Game/GUI/{relativePatchFilePath.Replace(".xml", ".xaml")}";

						var packagedPatchFile = package.Files.Find(x => x.Name == packagedPatchFilePath && !x.IsDeletion());
						var patchFilePath = Path.Combine(tmpModDir.FullName, Path.GetRandomFileName());

						using (var fs = new FileStream(patchFilePath, FileMode.Create, FileAccess.Write))
						{
							try
							{
								packagedPatchFile.MakeStream().CopyTo(fs);
							}
							finally
							{
								packagedPatchFile.ReleaseStream();
							}
						}

						if (!patches.ContainsKey(xamlFilePath))
							patches.Add(xamlFilePath, new List<(DivinityModData, string, string)>());
						patches[xamlFilePath].Add((mod, packagedPatchFilePath, patchFilePath));
					}
					catch (Exception ex)
					{
						E.Add(new XamlPatcherError(mod, packagedPatchFilePath, ex.ToString()));
					}
				}
			}
		}

		private static XDocument LoadPatchDocument(string patchFilePath, XDocument xamlDocument)
		{
			XDocument patchDocument = null;

			try
			{
				patchDocument = XDocument.Load(patchFilePath);
			}
			catch
			{
				// If the patch fails to load, try to fix its namespace declarations by copying them from the xaml document
				if (xamlDocument.Root != null)
				{
					var namespaces = xamlDocument.Root.Attributes()
					   .Where(x => x.IsNamespaceDeclaration)
					   .Select(x => $"xmlns:{x.Name.LocalName}=\"{x.Value}\"");

					var xml = File.ReadAllText(patchFilePath, Encoding.UTF8);
					xml = new Regex(@"^<diff ").Replace(xml, $"<diff {string.Join(" ", namespaces)} ", 1);

					patchDocument = XDocument.Parse(xml);
				}
			}

			return patchDocument;
		}
	}
}
