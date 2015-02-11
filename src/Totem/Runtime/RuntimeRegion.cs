﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Totem.Runtime
{
	/// <summary>
	/// A set of related areas in a Totem runtime
	/// </summary>
	public sealed class RuntimeRegion : Notion
	{
		public RuntimeRegion(RuntimeRegionKey key, IReadOnlyList<RuntimePackage> packages)
		{
			Key = key;
			Packages = packages;
		}

		public RuntimeRegionKey Key { get; private set; }
		public IReadOnlyList<RuntimePackage> Packages { get; private set; }

		public override Text ToText()
		{
			return Key.ToText();
		}

		public AreaType GetArea(RuntimeTypeKey key, bool strict = true)
		{
			var area = Packages
				.Select(package => package.GetArea(key, strict: false))
				.FirstOrDefault(packageArea => packageArea != null);

			Expect(strict && area == null).IsFalse("Failed to resolve area", key.ToText());

			return area;
		}

		public AreaType GetArea(Type declaredType, bool strict = true)
		{
			var area = Packages
				.Select(package => package.GetArea(declaredType, strict: false))
				.FirstOrDefault(packageArea => packageArea != null);

			Expect(strict && area == null).IsFalse("Failed to resolve area", Text.Of(declaredType));

			return area;
		}
	}
}