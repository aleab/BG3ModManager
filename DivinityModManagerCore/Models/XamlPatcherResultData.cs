using System.Collections.Generic;
using System.Linq;
using Tizuby.XmlPatchLib;

namespace DivinityModManager.Models
{
	public class XamlPatcherResultData
	{
		public DivinityModData MergeMod { get; }
		public IReadOnlyCollection<XmlPatcherError> Errors { get; }

		public XamlPatcherResultData(DivinityModData mergeMod, IEnumerable<XmlPatcherError> errors)
		{
			MergeMod = mergeMod;
			Errors = errors.ToList();
		}
	}
}
