namespace DivinityModManager.Models.XamlPatcher
{
	public class XamlPatcherError
	{
		public DivinityModData Mod { get; }
		public string PatchFile { get; }
		public string Error { get; }

		public XamlPatcherError(DivinityModData mod, string patchFile, string error)
		{
			Mod = mod;
			PatchFile = patchFile;
			Error = error;
		}
	}
}
