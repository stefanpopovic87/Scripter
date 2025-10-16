using System.Reflection;

namespace Scripter
{
	public sealed class IconProvider : IIconProvider
	{
		private readonly Dictionary<string, Image> _cache = new(StringComparer.OrdinalIgnoreCase);
		private Image? _logo;
		private static string[]? _resourceNames;

		public Image Get(string name, int size)
		{
			string key = $"{name}@{size}";
			if (_cache.TryGetValue(key, out var existing))
				return existing;

			var asm = Assembly.GetExecutingAssembly();
			_resourceNames ??= asm.GetManifestResourceNames();

			string desiredSuffix = $".Resources.icons.{name}.png";
			string? resource = _resourceNames.FirstOrDefault(r => r.EndsWith(desiredSuffix, StringComparison.OrdinalIgnoreCase));

			Stream? stream = resource != null ? asm.GetManifestResourceStream(resource) : null;

			if (stream == null)
			{
				string path = Path.Combine(AppContext.BaseDirectory, "Resources", "icons", $"{name}.png");
				if (File.Exists(path))
					stream = File.OpenRead(path);
			}

			if (stream == null)
				throw new FileNotFoundException($"Icon not found: {name}.png");

			using (stream)
			using (var img = Image.FromStream(stream))
			{
				var scaled = new Bitmap(img, new Size(size, size));
				_cache[key] = scaled;
				return scaled;
			}
		}

		public Image? GetLogo()
		{
			if (_logo != null) return _logo;

			var asm = Assembly.GetExecutingAssembly();
			_resourceNames ??= asm.GetManifestResourceNames();

			// Support both naming styles
			var suffixes = new[]
			{
				".Resources.scripter-logo.png"
			};

			string? resource = _resourceNames.FirstOrDefault(r =>
				suffixes.Any(s => r.EndsWith(s, StringComparison.OrdinalIgnoreCase)));

			if (resource != null)
			{
				using var s = asm.GetManifestResourceStream(resource);
				if (s != null)
				{
					_logo = Image.FromStream(s);
					return _logo;
				}
			}

			// Disk fallback
			string disk1 = Path.Combine(AppContext.BaseDirectory, "Resources", "scripter-logo.png");
			string disk2 = Path.Combine(AppContext.BaseDirectory, "Resources", "scripter.logo.png");
			string? disk = File.Exists(disk1) ? disk1 : (File.Exists(disk2) ? disk2 : null);
			if (disk != null)
			{
				_logo = Image.FromFile(disk);
			}

			return _logo;
		}
	}
}