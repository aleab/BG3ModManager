using System.Collections.Generic;
using System.Linq;
using Tizuby.XmlPatchLib;

namespace DivinityModManager.Models.XamlPatcher
{
	public class XamlPatcherResultData
	{
		public string MergeModFilePath { get; }
		public IReadOnlyCollection<XmlPatcherError> XmlPatcherErrors { get; }
		public IReadOnlyCollection<XamlPatcherError> InternalErrors { get; }

        public XamlPatcherResultData(string mergeModFilePath, IEnumerable<XmlPatcherError> xmlPatcherErrors, IEnumerable<XamlPatcherError> errors)
		{
			MergeModFilePath = mergeModFilePath;
			XmlPatcherErrors = xmlPatcherErrors.ToList();
			InternalErrors = errors.ToList();
		}
	}
}
