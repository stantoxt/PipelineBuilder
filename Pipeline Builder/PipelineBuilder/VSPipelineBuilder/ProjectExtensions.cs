using System;
using System.IO;
using EnvDTE;

namespace VSPipelineBuilder
{
	public static class ProjectExtensions
	{
		public static string Combine(this string path1, string path2)
		{
			if (path2 == null) throw new ArgumentNullException("path2");

			return Path.Combine(path1, path2);
		}


		public static bool ContainsFolder(this ProjectItems items, string folderName)
		{
			if (folderName == null) throw new ArgumentNullException("folderName");

			foreach (var item in items)
			{
				if (((ProjectItem)item).Name == folderName)
					return true;
			}
			return false;
		}
	}
}